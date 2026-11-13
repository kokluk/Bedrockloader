# BedrockLoader v0.17.11

## Türkçe

**Sürüm bilgisi**
- Üst çubuk, Hakkında ve Güncellemeler ekranı artık kurulu sürümü derlemeden okuyor. v0.17.9’da takılı kalan etiket düzeltildi.

**AMOLED teması**
- Ayarlar ve kurulum sihirbazına AMOLED eklendi. Koyu modda tam siyah arka plan, koyu gri paneller ve açık mavi vurgu kullanılıyor.

## English

**Version display**
- The header, About page, and updater now read the installed version from the build. The label that stayed at v0.17.9 has been fixed.

**AMOLED theme**
- Added AMOLED to Settings and the setup wizard. In dark mode, it uses a pure black background, dark gray panels, and a light blue accent.

<!-- end-release-notes -->

# BedrockLoader v0.17.10

## Türkçe

**Discord Rich Presence**
- Ana ekranda **Ana Menü · Boşta 🌙** gösterilir. Minecraft çalışırken paylaşım açıksa doğrulanmış sürüm, girilmişse yerel kullanıcı adı görünür.

**Arayüz ve güncelleyici**
- Seçilen tema sol alttaki durum alanında gösterilir.
- RAM göstergesi açıkken Windows özel bellek kullanımını bildiremezse uygulamanın toplam fiziksel bellek kullanımını gösterir.
- Güncelleme notları arayüz diline göre, bölümler halinde ve kaydırılabilir olarak gösterilir.

## English

**Discord Rich Presence**
- The launcher home screen shows **Main Menu · Idle 🌙**. While Minecraft is running, Discord shows the verified version if sharing is enabled and the local display name if configured.

**Interface and updater**
- The status area shows the selected theme.
- When enabled, the RAM badge falls back to the app’s total resident memory if Windows cannot report private resident memory.
- Update notes follow the launcher language and are organized into scrollable sections.

<!-- end-release-notes -->

# BedrockLoader v0.17.9 — Yama notları

## v0.17.6
- Güncellemeden sonraki ilk açılışta Yenilikler penceresi eklendi. Temiz kurulumda gösterilmez.
- Güncelleme onayı ve hata pencereleri launcher temasına uyarlandı.

## v0.17.7
- Gelişmiş Ayarlar’a çift onaylı uygulama verilerini silme ve uygulamayı kaldırma seçenekleri eklendi.
- Minecraft for Windows kurulumu Microsoft Store veya Xbox üzerinden yapılır. Launcher kurulum yönergesini ve mağaza bağlantısını gösterir.

## v0.17.8
- Cinnabar için ayrı Modlar kategorisi ve tıklanabilir ana ekran kartı eklendi. WASM dosyaları sürükle-bırak veya dosya seçiciyle eklenebilir, seçilen dosyalar Geri Dönüşüm Kutusu’na taşınabilir. Etkinleştirme ve izinler Cinnabar’dan yönetilir.
- Özelleştirme ve Discord RPC pencerelerinde kontrol alanları genişletildi. Ayarlar düğmesine yazı eklendi, dil düğmesi soluna taşındı ve sürüm yazısı kalınlaştırıldı.
- Parlama sol kategorilere uygulanmıyor. RAM göstergesi etkinleştirilirse tüm sayfalarda gösteriliyor. CPU göstergesi kaldırıldı.
- Modlar filtresinde “Diğer dosyaları göster” kullanılıyor. Sunucu kaynakları için iki ayrı düğme var.
- Skin katmanı anahtarı Favori’nin yanına taşındı. Skin seçili değilken devre dışı.
- Tema ve dil menülerinde kapatma animasyonu eklendi. Metin kutuları seçili metnin sürüklenmesini destekliyor.
- Kayıtlar ekranından launcher günlükleri ve mevcut Cinnabar hata raporları ZIP olarak dışa aktarılabiliyor.

## v0.17.9
- Tema ve dil menüleri seçim yapıldığında da kapanış animasyonu oynatıyor.
- Discord RPC anahtarları ve işlem düğmeleri arasındaki boşluklar düzenlendi. Fare üzerine gelince işlem ipucu görünüyor.
- Tema değişince sayfa simgeleri ve kaydırma çubuğu yeni vurgu rengiyle çiziliyor.

# BedrockLoader v0.17.3

Bu notlar v0.16.10’dan sonra, v0.16.11 ile v0.17.3 arasında gelen değişiklikleri kapsar.

## Minecraft ve Cinnabar

- Launcher’ın kendi dosyalarında bulunan Cinnabar kurulumu algılanıyor. Güncelleme bulunduğunda **Cinnabar’ı güncelle** gösteriliyor ve kurulu sürümü başlatma seçeneği sunuluyor.
- Cinnabar indirme ve güncelleme yüzdesi ana düğmede ve Windows görev çubuğunda gösteriliyor. Harici Windows kurulum ilerleme penceresi kaldırıldı. Minecraft’ın Microsoft Store kurulumunda görev çubuğu hareketli ilerleme göstergesi kullanıyor.
- Cinnabar güncelleme denetimi ana ekranın sağ üstüne taşındı. Minecraft düğmesi, launcher yeniden odaklandığında oyun kurulum durumunu kontrol ediyor.
- Minecraft ve Cinnabar arasındaki geçiş düğmesi gidilecek istemcinin adını gösteriyor ve simgesi tıklanınca dönüyor. **Sürüm farkları** iki istemcide de ikinci kısayol. **Cinnabar nedir?** Cinnabar modunda bulunuyor.
- Cinnabar kaynak paketi sayacı, manifest.json içeren klasörleri ve ZIP / MCPACK dosyalarını sayıyor. JSON gibi yardımcı dosyalar sayıya eklenmiyor.

## Ana ekran ve görsel düzen

- Mod ve paket sayaçlarına simgeler eklendi. Ana ekran kartları tıklanabiliyor ve ilgili ekranı ya da Cinnabar klasörünü açıyor. Eski **Modları gör** ve **Packleri gör** düğmeleri kaldırıldı.
- Modloader ve sürüm farkı kısayollarına temaya uygun tek renkli simgeler eklendi. Başlat simgesi dolu siyah ok olarak yenilendi.
- Ana ekran, Packler, Shaderlar, Ayarlar, Kayıtlar ve Güncelleyici simgeleri yenilendi. Güncelleyici simgesi tıklanınca dönüyor. Packler için kutu simgesi kullanılıyor ve **Hakkında** sol menüde ayırıcılarla gösteriliyor.
- Tıklanabilir düğmeler el imleci kullanıyor. Düğme ve kategori animasyonları, fare yönünü takip eden parlama ve gelişmiş simge hareketleri eklendi. Basılı tutup bırakınca kalan vurgu yaklaşık iki saniye sonra kayboluyor.
- Dil seçici üst çubuğa taşındı, daha yuvarlak hale getirildi ve aşağı doğru açılıyor. Görünüm düğmesi sağ altta, yazılı **Kayıtlar** düğmesi yanında.
- Kategori başlıkları sola hizalanıyor. Yerleşim sorunları giderildi ve yazı tipi Segoe UI’ye döndürüldü. Dünyalardaki **Klasörü aç** düğmesine kenarlık eklendi.
- Ana ekran, saat ve güne göre uygun seçeneklerden bir selamlama seçiyor. Örneğin **Günaydın**, **Tekrar merhaba**, **İyi geceler** veya **Harika bir cumartesi olsun**. Yerel ad girilmişse selamlamada gösteriliyor.

## Ayarlar, sihirbaz ve sistem tepsisi

- Launcher tek örnek çalışıyor. Tekrar başlatıldığında mevcut pencere açılıyor. Kapatma varsayılan olarak sistem tepsisine indiriyor. İlk indirmede Windows bildirimi gönderiliyor.
- Kurulum sihirbazı canlı tema önizlemesi, Windows / açık / koyu görünüm, Material You renkleri, Özelleştirme ve ayrı Discord RPC sayfası içeriyor. İsteğe bağlı yerel ad ikinci sayfada giriliyor. Sihirbaz Kısayollar’dan yeniden açılabiliyor.
- Ayarlar’daki ayrı **Görünüm** satırı kaldırıldı. **Blue** tema kaldırıldı, eski Blue ayarları **Midnight** olarak açılıyor.
- **Özelleştirme** penceresi iki sütunlu. Parlama, temel ve gelişmiş animasyonlar, tıklama sesi, RAM/CPU ve sistem tepsisi davranışı buradan ayarlanıyor. Anahtarlar büyütüldü ve kayma animasyonu eklendi.
- Seçeneklerin üzerine fareyle gelince açıklama çıkıyor. Tıklama sesi yeni kurulumlarda kapalı geliyor ve açıldığı anda örnek ses çalıyor.
- **Gelişmiş ayarları göster** düğmesi Discord RPC, Yedekler, Content klasörü ve Kısayollar’ı açıyor. Ayırıcı iki sütun boyunca uzanıyor. Content klasörü, Kısayollar’ın hemen üstünde ve kendi **Kaydet** düğmesine sahip. Cinnabar modunda kurulum yolu gösteriliyor.
- Ayarlar’da temaya uyumlu, ince ve yuvarlak kaydırma çubuğu var. İçerik pencereye sığdığında çubuk gizleniyor.
- Discord RPC etkinse, isteğe bağlı yerel ad Minecraft etkinliğine ekleniyor.

## Yedekleme, kayıtlar ve kaynak kullanımı

- Ayarlar ve kayıtlı sunucular JSON olarak dışa ve içe aktarılabiliyor.
- Tam ZIP yedeğine Minecraft dünyaları, kaynak ve davranış paketleri, skinler, yerel modlar ve Cinnabar verileri ile shader’ları seçilerek eklenebiliyor.
- İçe ve dışa aktarmadan önce hangi öğelerin işleneceği seçiliyor. Büyük oyun verileri varsayılan olarak seçili gelmiyor. Geri yüklemede mevcut oyun dosyaları korunuyor.
- Yeni kayıtlar seçili dilde yazılıyor. Son 300 satır tutuluyor ve sınır aşılınca yalnızca en eski kayıt siliniyor.
- İsteğe bağlı RAM ve CPU rozetleri ana ekranın üst çubuğunda. RAM için sürecin özel fiziksel belleği ölçülüyor. Rozetler varsayılan olarak kapalı ve dar pencerede taşmıyor.

## Modlar, sunucular ve skinler

- Modlar bölümünün dosya bırakma alanına **Dosya seç ve yükle** düğmesi eklendi.
- Kayıtlı sunucu kartlarında sağa hizalı çöp kutusu ve silme onayı bulunuyor. Deneme olarak eklenen çevrimiçi sunucu tarayıcısı kaldırıldı.
- Skin seçili değilken önizlemede soru işareti gösteriliyor. **Düzenle**, **Dışa aktar** ve **Skin uygula** düğmeleri devre dışı kalıyor.
- Skin klasörü ve favori simgeleri yenilendi. Skin düzenleyicide yuvarlak renk çarkı, daha fazla renk, palet ve çizim alanı arasında ayırıcı var. Katman düğmesi altta ve açma/kapama durumunu iki saniye gösteriyor.

Windows x64 kurulumu için sürümün **Assets** bölümündeki EXE dosyasını kullan. SHA-256 doğrulama dosyası aynı bölümde bulunur.

## Katkı

Geliştirme, test ve sürüm notlarına OpenAI Codex katkı sağladı.
