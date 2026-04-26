# VKFoodArea Demo Runbook - Band 9

## Muc tieu

He thong phai trinh dien duoc mot vong end-to-end: web co du lieu that de doc, app quet QR hoac nhan GPS, app phat thuyet minh, du lieu phan hoi sync ve web, admin xem dashboard/map/history, va co cach xu ly khi demo gap loi mang, GPS hoac camera.

## Luong uu tien 1-5

1. GPS/geofence demo on dinh
   - Mo app, vao ban do, cham tieu de 5 lan de hien nut `Mau`.
   - Chon `Oc Vu`, `Oc Thao`, `Oc Oanh`, hoac `Chay lo trinh mau`.
   - Ky vong: ban do nhay toi diem mau, trang thai demo hien ro, app tu phat diem gan nhat va web nhan movement log.

2. Du lieu dashboard/map
   - Web chi seed noi dung van hanh can thiet: POI, QR item va tour mau.
   - Dashboard/map khong duoc nhoi san telemetry gia trong initializer.
   - Du lieu phan hoi phai den tu app that hoac smoke test API: movement log, narration history, QR, GPS, Tour, Manual.

3. QR -> App -> nghe -> sync web
   - Mo web QR, quet `poi:oc-vu` hoac `tour:vinh-khanh-30-phut`.
   - Ky vong: app resolve dung POI/tour, phat audio/TTS, web ghi `triggerSource=qr` hoac `tour`.
   - Deep link QR cung duoc ghi la `qr`, khong bi tinh sai thanh manual.

4. Kiem thu nhanh truoc demo
   - Build web: `dotnet build .\VKFoodArea.Web\VKFoodArea.Web.csproj --no-restore -p:UseAppHost=false`
   - Compile app Android: `dotnet build .\VKFoodArea\VKFoodArea.csproj -f net10.0-android --no-restore -t:Compile`
   - Chay smoke API khi web dang bat: `.\scripts\demo-smoke-test.ps1 -BaseUrl http://localhost:5000`

5. Rui ro demo va phuong an du phong
   - GPS yeu: dung `Mau` tren ban do.
   - Camera khong quet duoc QR tren cung dien thoai: mo QR o laptop hoac dung link `/qr/{code}`.
   - Web/API sai base URL: giu nut `Web` trong man hinh QR de nhap lai endpoint demo.
   - Mang loi: app van dung POI local; sau do chay smoke test hoac demo GPS de tao phan hoi web.
   - Hoi dong hoi phan quyen: tao Admin/Restaurant Owner trong CMS, gan owner cho POI, sau do dang nhap owner de kiem tra owner chi xem POI/lich su thuoc minh.

## Tieu chi giang vien de cham cao

- Co du lieu phan hoi nhin duoc ngay tren dashboard, khong chi CRUD tinh.
- Co phan quyen thuc te giua admin va chu quan.
- Co test lap lai duoc bang command, khong phu thuoc hoan toan vao thao tac tay.
- Co fallback demo ro rang khi GPS/camera/mang loi.
- Co tu duy trien khai: initializer khong nhồi san feedback gia; du lieu dashboard phai sinh tu luong app/API co the lap lai.
