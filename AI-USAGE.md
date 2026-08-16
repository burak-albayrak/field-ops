# AI Kullanımım

Projenin planlanmasından deployment'ına kadar tüm aşamalarda AI'dan faydalandım.

## Kullandığım AI Araçları

### ChatGPT & Codex

Projenin planını tamamen Chatgpt 5.6 Sol ile yaptım.

Projenin kodlanmasında Chatgpt 5.6 Sol, Terra ve Luna'yı görevin ağırlığına/kapasitesine göre farklı effort'larda kullandım. 

Chatgpt mac desktop app'inde (eski adıyla codex) chatgpt agent'larını ve skill'lerini kullandım.

### Diğer modeller

Tüm planlamamı bitirdikten sonra farklı model yorumları dinleyebilmek adına planımı Claude, Gemini ve Grok'a yorumlattım. 
Bu modelleri sadece gözümden bir şey kaçma ihtimaline karşın kullandım. Development'da kullanmadım.

## AI'ı nasıl kullanıyorum?

AI'ı yazılım bilen bir arkadaşımla konuşuyor gibi kullanıyorum. Çoğu zaman yazılım/projelerim hakkında gerçek bir insanmış gibi konuşup sohbet ederim.

Uzun zamandır chatgpt kullandığım için chatgpt artık benim bir projeye nasıl yaklaştığımı, hangi adımlarımı atacağımı, nasıl ilerlemek istediğimi biliyor ve buna göre hareket ediyor.

Bu sebeple attığım spesifik bir bir prompt yok.

Bu projede de chatgpt'yi her zaman kullandığım gibi yani şu şekilde kullandım:

1- Detaylıca tüm projeyi tartıştık. 1. Günümün tamamı projeyi planlamakla geçti. A'dan Z'ye her şeyi düşünüp tasarladık.
AI'ın burada en büyük faydası olaya farklı açılardan bakmama yardımcı olması oldu.

2- Planlama bittikten sonra projeye start'ı her zaman kendim veririm, her şeyin kurulumunu kendim yaparım.

3- Sıra kodlamaya geldiğinde ise o zamanki güncel modelleri chatgpt'ye araştırtırtır ve o model için modelin geliştiricisinin önerilerini anlamasını sağlarım. 
(bu case'de her görev başında Chatgpt'ye codex tarafında hangi modeli hangi effort'da kullanalım kararını ona bıraktım. O araştırıp seçtiği modele özgü prompt hazırladı.)

4- Bir task agent tarafından tamamlandığında testlerin hepsi geçmiş olsa bile kodlara bakar, lokalimde postman ile çalıştırırım. 
Artık Agent'lar çok gelişti eskisi kadar hata yakalayamıyorum fakat buna devam etme sebebim bunu yapmadığım zaman projenin implementasyonundan kendimi çok soyutlanmış ve ipler elimden kayacakmış gibi hissetmem.

Bu proje boyunca AI, hatalı veya eksik bir fikirde/implementasyonda bulunmadı. Birçok yerde farklı fikirlerdeydik fakat fikirleri kendine göre mantıklıydı.
"Özellikle frontend'e create visit eklemeli miyiz?" konusunda çok tartıştık. Onun dışında verdiği önerileri tutarlıydı. Buna rağmen en son kararı her zaman ben verdim.