## Offline Çalışma – Teknik Tasarım

### Client-side Storage

Mobil uygulamada verilerin kalıcı olarak cihazda tutulması gerekir. Bunun için SQLite kullanılabilir. 

Native uygulamalarda iOS tarafında Core Data / SwiftData, Android tarafında ise Room gibi seçenekler de tercih edilebilir.

Ziyaret verilerinin yanında, internete gönderilmeyi bekleyen işlemler için de local bir queue tutulur.

Her işlem için örneğin şu bilgiler saklanabilir:

* OperationId
* VisitId
* OperationType (Start / Complete)
* ExpectedVersion
* ClientOccurredAtUtc
* Payload
* RetryCount

Bu kayıtların uygulama kapatılıp açılsa bile kaybolmaması gerekir.

### Synchronization

İnternet bağlantısı tekrar geldiğinde bekleyen işlemler sırayla server'a gönderilir.

Aynı Visit için birden fazla işlem varsa sırası korunmalıdır. Örneğin kullanıcı offline durumda önce ziyareti başlatıp sonra tamamladıysa, önce Start daha sonra Complete gönderilmelidir.

Başarılı olan işlemler local queue'dan silinir ve Visit'in güncel hali server'dan alınır.

### Duplicate Request

Aynı istek bağlantı problemi nedeniyle birden fazla kez gönderilebilir.

Bunu önlemek için her işlem client tarafında oluşturulan unique bir `OperationId` ile gönderilir. Server daha önce aynı `OperationId`'yi işlediyse işlemi tekrar uygulamak yerine önceki sonucu döndürür.

Bu sayede örneğin aynı Complete isteği tekrar geldiğinde `CompletedAt` değişmez ve ikinci kez Analytics eventi oluşturulmaz.

### Conflict Resolution

Client işlem yaparken Visit'in bildiği son `Version` bilgisini de gönderir.

Örneğin client Version 3'e sahipken başka bir kullanıcı Visit'i değiştirip Version 4 yaptıysa, eski Version 3 üzerinden gelen işlem doğrudan uygulanmamalıdır.

Server bu durumda conflict döner ve client güncel Visit bilgisini tekrar alır.

Eğer işlem yeni durumda hâlâ geçerliyse tekrar denenebilir. Geçerli değilse kullanıcıya conflict olduğu gösterilir.

### Timestamp

Client cihazının saati tamamen güvenilir kabul edilmemelidir.

Client işlemin yapıldığı zamanı `ClientOccurredAtUtc` olarak gönderir. Ancak server tarafındaki gerçek kayıt zamanı server tarafından oluşturulur ve UTC olarak saklanır.

Böylece client zamanı bilgi/audit amaçlı tutulurken sistemdeki asıl timestamp server tarafından belirlenmiş olur.

### Failed Synchronization

Hata türüne göre farklı davranılabilir:

* Network veya 5xx hatalarında daha sonra tekrar denenir.
* Conflict durumunda güncel veri alınır ve işlem tekrar değerlendirilir.
* Validation veya geçersiz status hatalarında sürekli retry yapılmaz.
* Authentication problemi varsa kullanıcı tekrar giriş yapana kadar sync bekletilir.

UI tarafında da işlemin `Pending`, `Synced` veya `Needs Attention` gibi durumları kullanıcıya gösterilebilir.
