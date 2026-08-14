\set ON_ERROR_STOP on

-- Örnek: psql -d fieldops_perf -v employee_id=1 -f scripts/performance/critical-query.sql

\if :{?employee_id}
\else
\set employee_id 1
\endif

-- Bu senaryodaki "Türkiye'deki çalışan" koşulu Employee ülkesidir; Store ülkesiyle karıştırılmaz.
SELECT
    v.id,
    v.employee_id,
    v.store_id,
    v.planned_date,
    v.status,
    v.started_at,
    v.completed_at,
    v.notes,
    v.version
FROM visits AS v
JOIN employees AS e
    ON e.id = v.employee_id
WHERE
    v.employee_id = :'employee_id'::bigint
    AND e.country_code = 'TR'
    AND v.status = 'Completed'
    AND v.completed_at >= CURRENT_TIMESTAMP - INTERVAL '30 days'
ORDER BY
    v.completed_at DESC,
    v.id DESC;
