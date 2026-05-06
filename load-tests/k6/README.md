# Hướng Dẫn Chạy Automation Test Và Load Test VKFoodArea

File này dùng UTF-8 và mô tả đầy đủ trình tự chạy web app, mở ngrok, chạy demo log, chạy automation test xUnit và chạy load test 1000 thiết bị ảo bằng 1000 `DeviceKey`.

## 1. Mục Tiêu Kiểm Thử

Các test được thiết kế để chứng minh 2 bài toán chính:

1. Khi người dùng đứng giữa 2 POI, app chọn POI theo đúng thuật toán hiện tại:
   - Ưu tiên POI gần nhất trước.
   - Nếu khoảng cách gần như bằng nhau, tức chênh lệch `<= 1m`, chọn POI có `Priority` cao hơn.
   - Vẫn tuân thủ `debounce`, `cooldown` và bán kính geofence.

2. Khi nhiều thiết bị cùng truy cập một POI:
   - Không dùng 1000 Android emulator thật.
   - Mô phỏng 1000 thiết bị bằng 1000 `DeviceKey` khác nhau.
   - Queue phát narration là `device-local`, không phải shared queue theo POI trên server.
   - Server chỉ nhận heartbeat/history theo từng thiết bị, không giữ hàng đợi phát audio chung cho POI.

## 2. Các File Liên Quan

```text
VKFoodArea.Domain.Tests/PoiScenarioLogTests.cs
VKFoodArea.Domain.Tests/ScenarioLog.cs
load-tests/k6/poi-api-load-test.js
load-tests/k6/README.md
```

Log xUnit sinh ra tại:

```text
artifacts/test-logs/poi-automation-scenarios.log
```

Log/summary k6 sinh ra tại:

```text
artifacts/load-tests/poi-api-load-test-summary.json
```

## 3. Bước 1 - Chạy Web App Ở Port 5216

Mở terminal thứ nhất tại thư mục project:

```powershell
cd C:\xampp\htdocs\VKFoodArea
dotnet run --project VKFoodArea.Web\VKFoodArea.Web.csproj --launch-profile http
```

Web app sẽ chạy ở:

```text
http://localhost:5216
```

Kiểm tra API POI:

```powershell
Invoke-WebRequest http://localhost:5216/api/pois -UseBasicParsing
```

Kết quả đúng là `StatusCode` bằng `200`. Nếu endpoint này chưa trả `200`, chưa chạy bước ngrok hoặc k6 vội.

## 4. Bước 2 - Chạy Ngrok Trỏ Vào Port 5216

Mở terminal thứ hai:

```powershell
ngrok http --domain=willow-unexposed-suing.ngrok-free.dev 5216
```

Kiểm tra API qua domain ngrok:

```powershell
Invoke-WebRequest `
  https://willow-unexposed-suing.ngrok-free.dev/api/pois `
  -Headers @{ "ngrok-skip-browser-warning"="true" } `
  -UseBasicParsing
```

Kết quả đúng là `StatusCode` bằng `200`.

Nếu trả `404 Not Found`, nghĩa là domain ngrok hiện chưa forward đúng vào web app `http://localhost:5216`. Khi đó hãy kiểm tra lại terminal ngrok và chắc chắn command đang dùng đúng domain, đúng port `5216`.

## 5. Bước 3 - Chạy Automation Test xUnit

Automation test xUnit chạy trực tiếp logic nghiệp vụ trong code hiện tại, không cần web app và không cần ngrok.

Chạy:

```powershell
cd C:\xampp\htdocs\VKFoodArea
dotnet test VKFoodArea.Domain.Tests\VKFoodArea.Domain.Tests.csproj
```

Xem log:

```powershell
Get-Content artifacts\test-logs\poi-automation-scenarios.log -Tail 120
```

Các dòng quan trọng trong log:

```text
[TEST-1A] Device nằm giữa Ốc Vũ và Ốc Loan, gần Ốc Vũ hơn nên phát Ốc Vũ
[TEST-1B] Hai POI gần như bằng nhau, Priority cao hơn thắng
[TEST-2] 1000 DeviceKey cùng truy cập 1 POI, queueScope=device-local
```

## 6. xUnit Test Đang Làm Gì?

### TEST-1A - Demo Analytics: Giữa Ốc Vũ Và Ốc Loan

Test dùng đúng kịch bản demo trên bản đồ analytics: 1 thiết bị nằm trong vùng giao nhau giữa `Ốc Vũ` và `Ốc Loan`.

```text
Device: map-analytics-demo-device-0001
Vị trí demo: 10.7613275, 106.7026730
POI 2: Ốc Vũ, radius 18m, priority 9
POI 7: Ốc Loan, radius 16m, priority 4
```

Log khoảng cách mẫu:

```text
Ốc Vũ:  khoảng 9.03m, trong vùng
Ốc Loan: khoảng 12.46m, trong vùng
```

Kỳ vọng:

```text
selectedPoi=2
expected="POI 2 - Ốc Vũ"
actual="POI 2 - Ốc Vũ"
```

Điều này chứng minh khi device nằm giữa 2 vùng geofence, thuật toán chọn POI gần nhất trước. Trong demo thực tế này, POI được phát là `Ốc Vũ`.

### TEST-1B - Near-Tie Thì Priority Cao Hơn Thắng

Test đặt người dùng ở giữa 2 POI, khoảng cách tới 2 POI gần như bằng nhau:

```text
distance gap <= 1m
```

Kỳ vọng:

```text
selectedPoi=102
```

Vì POI 102 có `Priority` cao hơn.

### TEST-2 - 1000 Thiết Bị Ảo Cùng Một POI

Test tạo 1000 `DeviceKey`:

```text
virtual-device-0001
virtual-device-0002
...
virtual-device-1000
```

Tất cả cùng request vào một POI:

```text
samePoi=201
```

Kỳ vọng:

```text
totalDevices=1000
successfulRequests=1001
failedRequests=0
concurrentFirstWaveStarts=1000
queueScope=device-local
```

Test có thêm request thứ 2 cho `virtual-device-0001`. Request này phải đợi queue local của chính device đó, nhưng 999 device còn lại không phải đợi. Đây là điểm chứng minh queue không phải shared queue theo POI trên server.

## 7. Bước 4 - Kiểm Tra k6

Kiểm tra k6:

```powershell
k6 version
```

Nếu PowerShell chưa nhận `k6`, dùng đường dẫn đầy đủ:

```powershell
& "C:\Program Files\k6\k6.exe" version
```

Nếu cần thêm vào PATH:

```powershell
$env:Path = "$env:Path;C:\Program Files\k6"
```

Sau đó đóng PowerShell và mở lại để PATH mới có hiệu lực lâu dài.

## 8. Bước 5 - Chạy Demo k6 5 Thiết Bị Qua Localhost

Chạy thử nhỏ trước:

```powershell
cd C:\xampp\htdocs\VKFoodArea

$env:DEVICES="5"
$env:VUS="5"
$env:API_BASE_URL="http://localhost:5216"

k6 run load-tests/k6/poi-api-load-test.js
```

Nếu PowerShell chưa nhận `k6`:

```powershell
& "C:\Program Files\k6\k6.exe" run load-tests/k6/poi-api-load-test.js
```

Kết quả pass mẫu:

```text
virtualDevices: 5
totalRequests: 10
failedRequestRate: 0
heartbeatSuccess: 5
heartbeatFailed: 0
narrationSuccess: 5
narrationFailed: 0
```

Vì mỗi thiết bị gửi 2 request:

```text
POST /api/device-presence/heartbeat
POST /api/narration-histories
```

## 9. Bước 6 - Chạy k6 1000 Thiết Bị Qua Localhost

```powershell
cd C:\xampp\htdocs\VKFoodArea

$env:DEVICES="1000"
$env:VUS="100"
$env:API_BASE_URL="http://localhost:5216"

k6 run load-tests/k6/poi-api-load-test.js
```

Hoặc:

```powershell
& "C:\Program Files\k6\k6.exe" run load-tests/k6/poi-api-load-test.js
```

Kỳ vọng:

```text
virtualDevices: 1000
totalRequests: 2000
heartbeatSuccess: 1000
heartbeatFailed: 0
narrationSuccess: 1000
narrationFailed: 0
failedRequestRate: 0
```

Ghi chú quan trọng:

```text
DEVICES=1000 nghĩa là tạo 1000 DeviceKey khác nhau.
VUS=100 nghĩa là tối đa 100 virtual users chạy đồng thời.
```

Không bắt buộc đặt `VUS=1000` để chứng minh 1000 thiết bị ảo. Nếu đặt `VUS=1000`, toàn bộ request sẽ nổ cùng lúc, đây là stress test rất gắt cho web app đang chạy local bằng `dotnet run` và database SQLite/dev. Khi đó có thể gặp lỗi `connection refused`, timeout hoặc database lock dù logic 1000 `DeviceKey` vẫn đúng.

## 10. Bước 7 - Chạy k6 Qua Ngrok

Chỉ chạy bước này sau khi `/api/pois` qua ngrok đã trả `200`.

```powershell
cd C:\xampp\htdocs\VKFoodArea

$env:DEVICES="1000"
$env:VUS="1000"
$env:API_BASE_URL="https://willow-unexposed-suing.ngrok-free.dev"

k6 run load-tests/k6/poi-api-load-test.js
```

Script tự gửi header:

```text
ngrok-skip-browser-warning: true
```

Header này giúp tránh trang cảnh báo của ngrok free khi gọi API.

## 11. Tuỳ Chỉnh POI Khi Chạy k6

Mặc định script dùng:

```text
POI_ID=1
POI_NAME=Oc Oanh
QR_CODE=poi:oc-oanh
```

Nếu muốn test POI khác:

```powershell
$env:POI_ID="4"
$env:POI_NAME="Ớt Xiêm Quán"
$env:QR_CODE="poi:ot-xiem-quan"
```

Sau đó chạy lại k6.

## 12. k6 Script Đang Làm Gì?

Trong `poi-api-load-test.js`, mỗi iteration tạo một device riêng bằng:

```text
virtual-device-0001
virtual-device-0002
...
virtual-device-1000
```

Script dùng `exec.scenario.iterationInTest` để bảo đảm mỗi iteration có index duy nhất trên toàn bộ test. Điểm này quan trọng vì nếu dùng `__ITER`, nhiều VU có thể bị trùng `DeviceKey`.

Mỗi device gửi heartbeat:

```text
POST /api/device-presence/heartbeat
```

Payload gồm:

```text
DeviceKey
UserKey
Username
FullName
Platform
DeviceName
AppVersion
IsOnline
```

Sau đó gửi narration history:

```text
POST /api/narration-histories
```

Payload gồm:

```text
PoiId
PoiName
QrCode
UserKey
Language
TriggerSource
Mode
PlayedAt
DurationSeconds
```

## 13. Cách Đọc Kết Quả

Các chỉ số pass:

```text
heartbeatFailed = 0
narrationFailed = 0
failedRequestRate = 0
```

Nếu `heartbeatFailed > 0`, kiểm tra:

```text
Web app còn chạy không?
Port có đúng 5216 không?
Database có lock hoặc lỗi ghi session không?
DeviceKey có bị trùng không?
```

Nếu log k6 có lỗi:

```text
connectex: No connection could be made because the target machine actively refused it
```

thì nguyên nhân là k6 không kết nối được tới web app tại thời điểm request chạy. Cách xử lý:

```powershell
Invoke-WebRequest http://localhost:5216/api/pois -UseBasicParsing
```

Phải thấy `StatusCode=200` rồi mới chạy k6. Script cũng có bước `setup()` tự kiểm tra `/api/pois` trước khi bắn tải, nhưng nếu web app bị dừng/restart trong lúc test thì vẫn sẽ fail.

Với máy local, nên chạy:

```powershell
$env:DEVICES="1000"
$env:VUS="100"
$env:API_BASE_URL="http://localhost:5216"
k6 run load-tests/k6/poi-api-load-test.js
```

Chỉ dùng `VUS=1000` khi muốn stress test mức cực đại và web/database đã đủ ổn định.

Nếu `narrationFailed > 0`, kiểm tra:

```text
POI_ID có tồn tại không?
POI có IsActive không?
POI có ApprovalStatus=Approved trên web không?
QrCode/PoiName có khớp dữ liệu web không?
```

Nếu chạy qua ngrok bị fail, kiểm tra:

```text
Ngrok có forward đúng port 5216 không?
/api/pois qua ngrok có trả 200 không?
Domain có đúng willow-unexposed-suing.ngrok-free.dev không?
```

## 14. Khác Nhau Giữa xUnit Và k6

xUnit chứng minh logic nghiệp vụ:

```text
GeofenceEngine
HaversineDistanceCalculator
Priority tie-break
Debounce
Cooldown
NarrationQueuePolicy.QueueScope
Device-local queue behavior
```

k6 chứng minh API chịu được nhiều thiết bị ảo gửi request thật:

```text
1000 DeviceKey
1000 heartbeat request
1000 narration history request
Không cần emulator
Không tạo shared POI queue trên server
```

Vì queue phát audio nằm phía app/device, k6 không phát audio thật. k6 chỉ chứng minh server nhận dữ liệu từ nhiều thiết bị và lưu history/presence theo từng key riêng.

## 15. Trình Tự Chạy Chuẩn Từ Đầu Đến Cuối

Chạy web:

```powershell
dotnet run --project VKFoodArea.Web\VKFoodArea.Web.csproj --launch-profile http
```

Kiểm tra local:

```powershell
Invoke-WebRequest http://localhost:5216/api/pois -UseBasicParsing
```

Chạy ngrok:

```powershell
ngrok http --domain=willow-unexposed-suing.ngrok-free.dev 5216
```

Kiểm tra ngrok:

```powershell
Invoke-WebRequest `
  https://willow-unexposed-suing.ngrok-free.dev/api/pois `
  -Headers @{ "ngrok-skip-browser-warning"="true" } `
  -UseBasicParsing
```

Chạy xUnit automation test:

```powershell
dotnet test VKFoodArea.Domain.Tests\VKFoodArea.Domain.Tests.csproj
```

Xem xUnit log:

```powershell
Get-Content artifacts\test-logs\poi-automation-scenarios.log -Tail 120
```

Chạy k6 demo 5 thiết bị:

```powershell
$env:DEVICES="5"
$env:VUS="5"
$env:API_BASE_URL="http://localhost:5216"
k6 run load-tests/k6/poi-api-load-test.js
```

Chạy k6 full 1000 thiết bị:

```powershell
$env:DEVICES="1000"
$env:VUS="100"
$env:API_BASE_URL="http://localhost:5216"
k6 run load-tests/k6/poi-api-load-test.js
```

Chạy k6 qua ngrok:

```powershell
$env:DEVICES="1000"
$env:VUS="1000"
$env:API_BASE_URL="https://willow-unexposed-suing.ngrok-free.dev"
k6 run load-tests/k6/poi-api-load-test.js
```

