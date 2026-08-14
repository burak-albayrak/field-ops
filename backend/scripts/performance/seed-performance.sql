\set ON_ERROR_STOP on

-- Örnek: psql -d fieldops_perf -v visit_count=1000000 -f scripts/performance/seed-performance.sql
-- employee_count ve store_count de aynı yöntemle değiştirilebilir; verilmezlerse production ölçek hedefleri kullanılır.

\if :{?visit_count}
\else
\set visit_count 100000
\endif

\if :{?employee_count}
\else
\set employee_count 50000
\endif

\if :{?store_count}
\else
\set store_count 100000
\endif

-- Bu fixture normal geliştirme verisini silmemelidir; yanlış veritabanında çalıştırılırsa TRUNCATE öncesinde durur.
DO $$
BEGIN
    IF current_database() <> 'fieldops_perf' THEN
        RAISE EXCEPTION
            'Performance seed yalnızca fieldops_perf veritabanında çalıştırılabilir; mevcut veritabanı: %',
            current_database();
    END IF;
END
$$;

BEGIN;

CREATE TEMP TABLE performance_seed_settings ON COMMIT DROP AS
SELECT
    :'visit_count'::bigint AS visit_count,
    :'employee_count'::bigint AS employee_count,
    :'store_count'::bigint AS store_count,
    date_trunc('second', CURRENT_TIMESTAMP) AS seeded_at;

DO $$
DECLARE
    settings performance_seed_settings%ROWTYPE;
BEGIN
    SELECT * INTO settings FROM performance_seed_settings;

    IF settings.visit_count < 1 THEN
        RAISE EXCEPTION 'visit_count en az 1 olmalıdır.';
    END IF;

    IF settings.employee_count < 2 THEN
        RAISE EXCEPTION 'employee_count en az 2 olmalıdır.';
    END IF;

    IF settings.store_count < 1 THEN
        RAISE EXCEPTION 'store_count en az 1 olmalıdır.';
    END IF;

    IF settings.visit_count > (settings.employee_count - 1) * settings.store_count THEN
        RAISE EXCEPTION
            'visit_count, aktif Visit anahtarlarını benzersiz tutan employee/store kapasitesini aşmamalıdır.';
    END IF;
END
$$;

TRUNCATE TABLE outbox_messages, visits, employees, stores RESTART IDENTITY;

INSERT INTO employees (id, name, email, country_code)
OVERRIDING SYSTEM VALUE
SELECT
    employee_number,
    'Performance Employee ' || employee_number,
    'performance-employee-' || employee_number || '@example.test',
    CASE
        WHEN employee_number = 1 THEN 'TR'
        WHEN employee_number % 4 = 0 THEN 'TR'
        WHEN employee_number % 4 = 1 THEN 'DE'
        WHEN employee_number % 4 = 2 THEN 'UK'
        ELSE 'AE'
    END
FROM performance_seed_settings AS settings
CROSS JOIN LATERAL generate_series(1, settings.employee_count) AS employee_number;

SELECT setval(
    pg_get_serial_sequence('employees', 'id'),
    (SELECT employee_count FROM performance_seed_settings),
    true);

INSERT INTO stores (id, name, country_code, latitude, longitude)
OVERRIDING SYSTEM VALUE
SELECT
    store_number,
    'Performance Store ' || store_number,
    CASE store_number % 4
        WHEN 0 THEN 'TR'
        WHEN 1 THEN 'DE'
        WHEN 2 THEN 'UK'
        ELSE 'AE'
    END,
    36.0 + ((store_number % 6000)::double precision / 1000.0),
    26.0 + ((store_number % 20000)::double precision / 1000.0)
FROM performance_seed_settings AS settings
CROSS JOIN LATERAL generate_series(1, settings.store_count) AS store_number;

SELECT setval(
    pg_get_serial_sequence('stores', 'id'),
    (SELECT store_count FROM performance_seed_settings),
    true);

-- Target çalışan terminal Visit'lerle ayrıca beslenir; diğer satırlar farklı çalışan, mağaza, durum ve tarihlere dağılır.
WITH settings AS
(
    SELECT
        *,
        LEAST(GREATEST(visit_count / 100, 100), 100000, visit_count) AS target_visit_count
    FROM performance_seed_settings
),
generated AS
(
    SELECT
        visit_number,
        settings.*,
        visit_number <= target_visit_count AS is_target,
        visit_number - target_visit_count AS regular_number
    FROM settings
    CROSS JOIN LATERAL generate_series(1, settings.visit_count) AS visit_number
),
classified AS
(
    SELECT
        *,
        CASE
            WHEN is_target AND visit_number % 5 = 0 THEN 'Cancelled'
            WHEN is_target THEN 'Completed'
            WHEN regular_number % 20 = 0 THEN 'Planned'
            WHEN regular_number % 20 = 1 THEN 'InProgress'
            WHEN regular_number % 20 BETWEEN 2 AND 15 THEN 'Completed'
            ELSE 'Cancelled'
        END AS visit_status,
        CASE
            WHEN is_target THEN 1
            ELSE 2 + ((regular_number - 1) % (employee_count - 1))
        END AS generated_employee_id,
        CASE
            WHEN is_target THEN 1 + ((visit_number - 1) % store_count)
            ELSE 1 + (((regular_number - 1) / (employee_count - 1)) % store_count)
        END AS generated_store_id,
        CASE
            WHEN is_target THEN
                seeded_at
                - (((visit_number - 1) % 60) * INTERVAL '1 day')
                - ((visit_number % 86400) * INTERVAL '1 second')
            ELSE
                seeded_at
                - (((regular_number - 1) % 365) * INTERVAL '1 day')
                - ((regular_number % 86400) * INTERVAL '1 second')
        END AS lifecycle_at
    FROM generated
)
INSERT INTO visits
(
    id,
    employee_id,
    store_id,
    planned_date,
    status,
    started_at,
    completed_at,
    start_latitude,
    start_longitude,
    notes,
    created_at,
    version
)
OVERRIDING SYSTEM VALUE
SELECT
    visit_number,
    generated_employee_id,
    generated_store_id,
    (lifecycle_at - INTERVAL '1 day')::date,
    visit_status,
    CASE
        WHEN visit_status IN ('InProgress', 'Completed') THEN lifecycle_at - INTERVAL '1 hour'
        ELSE NULL
    END,
    CASE WHEN visit_status = 'Completed' THEN lifecycle_at ELSE NULL END,
    CASE
        WHEN visit_status IN ('InProgress', 'Completed')
            THEN 36.0 + ((generated_store_id % 6000)::double precision / 1000.0)
        ELSE NULL
    END,
    CASE
        WHEN visit_status IN ('InProgress', 'Completed')
            THEN 26.0 + ((generated_store_id % 20000)::double precision / 1000.0)
        ELSE NULL
    END,
    CASE WHEN is_target AND visit_status = 'Completed' THEN 'Critical query target visit' ELSE NULL END,
    lifecycle_at - INTERVAL '2 days',
    CASE visit_status
        WHEN 'Planned' THEN 1
        WHEN 'InProgress' THEN 2
        WHEN 'Completed' THEN 3
        ELSE 2
    END
FROM classified;

SELECT setval(
    pg_get_serial_sequence('visits', 'id'),
    (SELECT visit_count FROM performance_seed_settings),
    true);

-- Bulk yükleme sonrası planner'ın üretilen dağılımı bilmesi için istatistikler EXPLAIN'den önce yenilenir.
ANALYZE employees;
ANALYZE stores;
ANALYZE visits;

COMMIT;

SELECT
    (SELECT count(*) FROM employees) AS employees,
    (SELECT count(*) FROM stores) AS stores,
    (SELECT count(*) FROM visits) AS visits,
    (SELECT count(*)
     FROM visits
     WHERE employee_id = 1
       AND status = 'Completed'
       AND completed_at >= CURRENT_TIMESTAMP - INTERVAL '30 days') AS target_recent_completed_visits;
