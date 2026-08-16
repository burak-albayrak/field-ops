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

Bu sebeple attığım spesifik bir prompt tarzı yok. 

Bu projede de chatgpt'yi her zaman kullandığım gibi yani şu şekilde kullandım:

1- Detaylıca tüm projeyi tartıştık. 1. Günümün tamamı projeyi planlamakla geçti. A'dan Z'ye her şeyi düşünüp tasarladık.
AI'ın burada en büyük faydası farklı durumlara farklı açılardan bakmama yardımcı olması oldu.

2- Planlama bittikten sonra projeye start'ı her zaman kendim veririm, her şeyin kurulumunu kendim yaparım.

3- Sıra kodlamaya geldiğinde ise o zamanki güncel modelleri chatgpt'ye araştırtırtır ve o model için modelin geliştiricisinin önerilerini anlamasını sağlarım. 
(bu case'de her görev başında Chatgpt'ye codex tarafında hangi modeli hangi effort'da kullanalım kararını ona bıraktım. O araştırıp seçtiği modele özgü prompt hazırladı.)

4- Bir task agent tarafından tamamlandığında testlerin hepsi geçmiş olsa bile kodlara bakar, lokalimde postman ile çalıştırırım. 

Planlama sırasında AI, bir Visit’in başlatılabilmesi için PlannedDate değerinin mağazanın bulunduğu timezone’daki mevcut gün ile aynı olması gerektiğini önerdi. 
Case’i tekrar kontrol ettiğimde böyle bir requirement olmadığını gördüm. Case yalnızca Visit’in Planned durumda olmasını ve mağazaya maksimum 200 metre uzaklık şartını belirtiyordu. 
Bu nedenle öneriyi reddettim. Burada ek bir business rule üretmek, verilen requirement’ı uygulamak yerine değiştirmek anlamına gelecekti.