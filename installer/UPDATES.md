# v0.13 güncelleme kanalı

Uygulamadaki üst sol simge veya Ayarlar > Güncellemeleri denetle, güncelleme sayfasını açar. Her uygulama açılışında arka planda bir kez sürüm bilgisi kontrol edilir. Yeni sürüm varsa üst sol simgenin sol üstünde kırmızı “1” bildirimi gösterilir. Kullanıcı ayrıca elle kontrol edebilir; otomatik indirme/kurulum ve zorunlu güncelleme yapılmaz. Güncelleme yoksa veya doğrulanabilir kurulum dosyası yayınlanmamışsa “İndir ve kur” düğmesi gizlenir.

Kanal: `https://api.github.com/repos/kokluk/Bedrockloader/releases/latest`

## Yeni sürüm yayınlama

1. Uygulama projesindeki Version alanını, arayüz sürümünü, `ReleaseUpdates.CurrentVersion` değerini ve Inno Setup sürüm alanlarını birlikte artır.
2. Kurulum dosyasını `installer/build.ps1` ile üret.
3. GitHub Releases'te kararlı bir release oluştur. Örnek etiket: `v0.14`; eşleşen dosya adı: `BedrockLoader-Setup-v0.14-x64.exe`. Üç bileşenli etiket kullanılıyorsa (`v0.14.0`) dosya adında da aynı sürüm bulunmalı.
4. Kurulum EXE dosyasını release asset olarak yükle. GitHub API'sindeki `digest` alanı `sha256:...` biçiminde dolu olmalı. Taslak ve ön sürümler güncelleme olarak sunulmaz.
5. Son kullanıcıların erişebildiği herkese açık bir kanal gerekir. Gizli depoya uygulama içinden token olmadan erişilemez; uygulamaya kişisel erişim anahtarı gömülmez. Mevcut uygulama deposu gizliyken yalnızca kaynak ZIP'ini yüklemek, çalışan bir güncelleme kanalı oluşturmaz. Depo görünürlüğünü değiştirmek sahibinin ayrı kararıdır.

Sadece belirtilen deponun HTTPS release bağlantıları kabul edilir. Boyut sınırı 300 MB'dır. Uygulama sürümü daha yeni değilse kurulum önerilmez. İndirme boyutu, SHA-256 özeti ve Windows EXE biçimi doğrulanmadan kurulum başlatılmaz. Kullanıcı onayından sonra indirme yapılır; doğrulama başarılıysa etkileşimli Setup açılır ve uygulama kapanır. Kurulu sürümde mevcut kurulum klasörü korunur. Kullanıcı modları ve ayarları kurulumun dosya listesine dahil değildir.

## Testler

Proje kökünden, bir Windows EXE dosyasını yalnızca biçim doğrulama örneği olarak ver:

```powershell
dotnet run --project tests/ReleaseUpdates.Tests -c Release -- installer-payload/BedrockLoader.exe
```

Testler ağ yerine sahte HTTP yanıtları kullanır; kurulum dosyası çalıştırmaz. Sürüm karşılaştırması, 404/403 yanıtları, taslak/ön sürüm reddi, kaynak URL kontrolü, hash eksikliği, boyut sınırı, bozuk/eksik indirme, geçici dosya temizliği, mevcut dosyanın korunması ve iptal senaryolarını kapsar.

## Dahili mod deposu

Modlar sayfasının sağ üstündeki “Dahili modları gör” düğmesi şu an “Depo yakında” sayfasını açar. Henüz depo adresi verilmediği için örnek veya sahte mod gösterilmez.
