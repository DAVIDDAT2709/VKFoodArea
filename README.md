# VKFoodArea

**VKFoodArea** là đồ án xây dựng hệ thống hướng dẫn khám phá ẩm thực đường phố Vĩnh Khánh (Quận 4, TP.HCM) dành cho khách du lịch và người dùng mới.

Hệ thống gồm:
- **Ứng dụng di động Android** viết bằng **.NET MAUI** để hỗ trợ người dùng tra cứu điểm ăn uống, nghe thuyết minh TTS, quét QR và trải nghiệm tour.
- **Web quản trị tích hợp API** viết bằng **ASP.NET Core MVC** để quản lý nội dung, quản lý POI, tour, mã QR, lịch sử nghe và dữ liệu phục vụ demo.

README này được trình bày theo hướng **ngắn gọn, logic, dễ đọc**, phù hợp để giảng viên có thể nắm nhanh mục tiêu, kiến trúc, chức năng và cách chạy dự án.

---

## 1. Mục tiêu dự án

Dự án được xây dựng nhằm giải quyết bài toán:
- Giới thiệu các điểm ăn uống trên phố ẩm thực **Vĩnh Khánh** một cách trực quan.
- Hỗ trợ khách du lịch **nghe thuyết minh tự động** khi đến gần một địa điểm.
- Cho phép **quét QR để mở nhanh nội dung** POI hoặc tour.
- Cung cấp **web quản trị** để cập nhật dữ liệu, quản lý nội dung và theo dõi hoạt động sử dụng.

---

## 2. Bài toán và giá trị học thuật

VKFoodArea không chỉ là một ứng dụng hiển thị danh sách quán ăn, mà là một mô hình hệ thống gồm nhiều thành phần phối hợp:
- **Mobile app** phục vụ trải nghiệm người dùng cuối.
- **CMS/Web quản trị** dành cho admin hoặc chủ cửa hàng.
- **API tích hợp trong web** để đồng bộ dữ liệu cho app.
- **Cơ chế geofence + GPS + QR** để mô phỏng trải nghiệm thực tế.
- **Lịch sử nghe và dữ liệu thiết bị** để phục vụ phân tích sử dụng.

Điểm học thuật nổi bật của đồ án:
- Kết hợp **phát triển ứng dụng di động** và **phát triển web** trong cùng một hệ thống.
- Ứng dụng **SQLite + Entity Framework Core** để quản lý dữ liệu.
- Sử dụng **Mapsui** để hiển thị bản đồ.
- Áp dụng tư duy **workflow thực tế**: dữ liệu nội dung → web quản trị → API → app → lịch sử sử dụng.

---

## 3. Kiến trúc hệ thống

```mermaid
flowchart LR
    U[Người dùng / Khách du lịch] --> A[Ứng dụng Android - .NET MAUI]
    A <--> W[Web quản trị + API - ASP.NET Core MVC]
    W <--> D[(SQLite Database)]
    M[Admin / Chủ cửa hàng] --> W
