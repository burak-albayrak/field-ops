# Code Review

Verilen kod çalışabilir ancak production ortamında bazı problemler oluşturabilir.

## Analytics çağrısı

En önemli problem Analytics servisinin `SaveChangesAsync()` çağrısından önce çalıştırılması.

Analytics isteği başarılı olup database kaydı başarısız olursa Analytics tarafında tamamlanmış görünen bir Visit, database'de hala `InProgress` kalabilir.

Aynı şekilde Analytics servisi çalışmıyorsa Visit de tamamlanamaz. Visit'in tamamlanması harici bir servise bağlı olmamalı.

Bunun için **Transactional Outbox** kullanılabilir. Visit'in tamamlanması ve Analytics'e gönderilecek event aynı database transaction içerisinde kaydedilir. Daha sonra background worker event'i Analytics servisine gönderir.

## Idempotency

Client isteği gönderdikten sonra cevabı alamazsa aynı Complete isteğini tekrar gönderebilir.

Visit zaten `Completed` ise tekrar `CompletedAt` değiştirilmemeli ve yeni Analytics eventi oluşturulmamalı. Mevcut sonuç tekrar döndürülebilir.

## Concurrency

İki kullanıcı aynı Visit üzerinde eski veriyle işlem yapabilir. Bunun için Visit üzerinde `Version` gibi bir optimistic concurrency alanı kullanılabilir.

Client'ın gönderdiği version database'deki version ile uyuşmuyorsa işlem uygulanmak yerine conflict dönülmelidir.

## DateTime

`DateTime.Now` server'ın bulunduğu timezone'a bağlıdır. Timestamp'lerin tutarlı olması için `DateTime.UtcNow` kullanılmalıdır.

## Status ve Exception

Status değerlerinin `"InProgress"` gibi string olarak karşılaştırılması yerine enum kullanılması daha güvenlidir.

Ayrıca her durumda `Exception` fırlatmak yerine uygun hatalar dönülebilir:

* Visit bulunamadı → `404`
* Geçersiz status / concurrency problemi → `409`

Özet olarak Complete işlemi database'e güvenli şekilde kaydedilmeli, Analytics işlemi background'da yapılmalı ve idempotency ile concurrency durumları ayrıca ele alınmalıdır.
