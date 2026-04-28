# FORMAX — FAZ 2.1 / 2.2 / 2.3 / API TEST REHBERİ

Bu pakette aşağıdakiler eklendi:
- Dev Mode: `formax-web/src/App.jsx` içinde summary debug paneli `VITE_DEV_PANELS` flag'i ile gizlendi.
- Raw event katmanı: `UserInterestEvent` + migration
- Tracking service: `UserInterestTrackingService`
- Use case: `TrackInterestEventUseCase`
- API: `POST /api/interests/track`

## 1) Migration çalıştır
Backend klasöründe:

```bash
cd Formax.API

dotnet ef database update --project ../Formax.Infrastructure --startup-project .
```

Beklenen sonuç:
- DB'de `UserInterestEvents` tablosu oluşur.

## 2) Backend'i ayağa kaldır

```bash
cd Formax.API

dotnet run
```

## 3) Login ile token al
Önce register, sonra login yap.

### Register
```http
POST /api/auth/register
Content-Type: application/json

{
  "email": "ahmetinterest1@formax.com",
  "password": "123456",
  "fullName": "Ahmet Interest"
}
```

### Login
```http
POST /api/auth/login
Content-Type: application/json

{
  "email": "ahmetinterest1@formax.com",
  "password": "123456"
}
```

Beklenen sonuç:
- response içinde token gelir.

## 4) Interest track endpoint testi
Header:
```http
Authorization: Bearer <TOKEN>
```

### Test A — match_view
```http
POST /api/interests/track
Content-Type: application/json
Authorization: Bearer <TOKEN>

{
  "eventType": "match_view",
  "matchId": 1
}
```

Beklenen sonuç:
- `200 OK`
- body: `{ "success": true }`
- `UserInterestEvents` tablosuna 1 satır eklenir.
- `UserInterestScores` içinde:
  - `Layer = Team` için maçın home/away takımlarına score eklenir
  - `Layer = League` için maçın ligi varsa score eklenir
  - `Layer = ContentType`, `Key = MatchView` için score eklenir

### Test B — match_click
```http
POST /api/interests/track
Content-Type: application/json
Authorization: Bearer <TOKEN>

{
  "eventType": "match_click",
  "matchId": 1
}
```

Beklenen sonuç:
- weight = 3
- aynı user için ilgili score'lar artar.

### Test C — depth_open
```http
POST /api/interests/track
Content-Type: application/json
Authorization: Bearer <TOKEN>

{
  "eventType": "depth_open",
  "matchId": 1
}
```

Beklenen sonuç:
- weight = 6
- Team / League / ContentType score'ları tekrar artar.

### Test D — team_follow
```http
POST /api/interests/track
Content-Type: application/json
Authorization: Bearer <TOKEN>

{
  "eventType": "team_follow",
  "teamId": 1
}
```

Beklenen sonuç:
- weight = 8
- `UserInterestEvents` satırı oluşur
- `UserInterestScores` içinde `Layer = Team` ilgili takım için artar
- `ContentType = TeamFollow` artar

### Test E — league_follow
```http
POST /api/interests/track
Content-Type: application/json
Authorization: Bearer <TOKEN>

{
  "eventType": "league_follow",
  "leagueName": "Türkiye - Süper Lig"
}
```

Beklenen sonuç:
- weight = 5
- `Layer = League` için ilgili lig score'u artar
- `ContentType = LeagueFollow` artar

## 5) Hatalı event testi
```http
POST /api/interests/track
Content-Type: application/json
Authorization: Bearer <TOKEN>

{
  "eventType": "unknown_event"
}
```

Beklenen sonuç:
- mevcut implementasyonda exception fırlatır
- HTTP 500 görürsen bu normaldir; bu, sonraki sertleştirme adımında 400'e çevrilebilir.

## 6) SQL doğrulama sorguları

### Son event kayıtları
```sql
SELECT TOP 20 *
FROM dbo.UserInterestEvents
ORDER BY Id DESC;
```

### İlgili user score'ları
```sql
SELECT *
FROM dbo.UserInterestScores
WHERE UserId = <USER_ID>
ORDER BY Layer, Score DESC;
```

## 7) Dev Mode testi (web)
Web klasöründe `.env` veya `.env.local` dosyasına şunu yaz:

```env
VITE_DEV_PANELS=false
```

Beklenen sonuç:
- home summary debug kutuları görünmez.

Açmak için:

```env
VITE_DEV_PANELS=true
```

Beklenen sonuç:
- summary debug kutuları tekrar görünür.
