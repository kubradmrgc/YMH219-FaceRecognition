# YMH219-FaceRecognition

OpenCV (OpenCvSharp) ile kameradan veya dosyadan alınan görüntüde yüz algılayan ve küçük, yerel bir örnek kümesiyle
temel yüz tanıma yapan C# Windows Forms uygulaması.

> Ders: Nesne Tabanlı Programlama (YMH 219) · Öğrenci: Kübra Demirgüç (230541115)

## Durum

Proje geliştirme aşamasındadır. Şu an uygulama iskeleti, arayüz yerleşimi ve OpenCV bağlantısı hazırdır.

## Kullanılan teknolojiler

- .NET 9, Windows Forms (x64)
- OpenCvSharp4 4.13.0.20261107 ve OpenCvSharp4.runtime.win 4.13.0.20261107
- Haar cascade (`haarcascade_frontalface_default.xml`) ile yüz algılama
- LBPH (Local Binary Patterns Histograms) ile yüz tanıma

## Çalıştırma

```powershell
git clone https://github.com/kubradmrgc/YMH219-FaceRecognition.git
cd YMH219-FaceRecognition
dotnet run --project FaceRecognition
```
