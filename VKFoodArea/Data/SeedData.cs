namespace VKFoodArea.Data;

public static class SeedData
{
    public static IReadOnlyList<SeedPoiData> Pois { get; } =
    [
        new SeedPoiData
        {
            Name = "Ốc Oanh",
            Address = "534 Đ. Vĩnh Khánh, Quận 4, TP.HCM",
            Latitude = 10.7607194,
            Longitude = 106.7032972,
            RadiusMeters = 22,
            Priority = 10,
            Description = "Nên gọi: ốc hương rang muối ớt, sò điệp nướng mỡ hành. Giá tham khảo: 70.000đ - 120.000đ/món. Đông nhất: 19:00 - 21:00. Điểm khác biệt: quán lớn, dễ nhận ra, hợp đi nhóm khi muốn thử nhiều món ốc trong một lượt.",
            TtsScriptVi = "Bạn đang ở gần Ốc Oanh. Nếu đi nhóm, hãy gọi ốc hương rang muối ớt và sò điệp nướng mỡ hành trước, vì đây là những món dễ ăn và hợp khẩu vị số đông. Quán thường đông sau 7 giờ tối, nên nếu thấy kín bàn bạn có thể lưu lại và ghé sớm hơn ở lượt sau.",
            TtsScriptEn = "You are approaching Oc Oanh, one of the most famous seafood restaurants on Vinh Khanh Street. The restaurant has been recommended by the Michelin Guide as a must-try spot when exploring Saigon’s street food scene. Fresh seafood is prepared in many styles such as stir-fried, grilled, or steamed. The lively outdoor atmosphere makes it a popular meeting place for friends and families in the evening.",
            TtsScriptZh = "您正在接近Ốc Oanh餐厅，这是永庆美食街最著名的海鲜餐厅之一。该餐厅曾被《米其林指南》推荐为探索西贡街头美食时值得一试的地方。这里的海鲜新鲜多样，可用炒、烤或煮等多种方式烹制。热闹的露天用餐环境使它成为朋友和家庭晚上聚会的热门地点。",
            TtsScriptJa = "あなたはオック・オアインに近づいています。ここはヴィンカイン通りで最も有名なシーフード店の一つで、ミシュランガイドにも紹介されたことがあります。新鮮な海鮮は、炒め物、焼き物、蒸し料理などさまざまな方法で調理されます。活気ある屋外の雰囲気は、夜に友人や家族と集まるのにぴったりの場所です。",
            TtsScriptDe = "Sie nähern sich dem Restaurant Oc Oanh, einem der bekanntesten Seafood-Lokale der Vinh-Khanh-Straße. Das Restaurant wurde sogar im Michelin Guide als empfehlenswerter Ort für Saigons Street-Food erwähnt. Frische Meeresfrüchte werden hier auf verschiedene Arten zubereitet, zum Beispiel gebraten, gegrillt oder gedämpft. Die lebhafte Atmosphäre im Freien macht es zu einem beliebten Treffpunkt für Freunde und Familien am Abend.",
            IsActive = true,
            ImageUrl = "ocoanh.webp",
            QrCode = "poi:oc-oanh"
        },
        new SeedPoiData
        {
            Name = "Ốc Vũ",
            Address = "37 Đ. Vĩnh Khánh, Quận 4, TP.HCM",
            Latitude = 10.7614025,
            Longitude = 106.7027047,
            RadiusMeters = 18,
            Priority = 9,
            Description = "Nên gọi: ốc xào me, nghêu hấp sả, càng ghẹ rang muối. Giá tham khảo: 60.000đ - 100.000đ/món. Đông nhất: sau 18:30. Điểm khác biệt: vị me chua ngọt rõ, hợp người thích món đậm và ăn nhanh.",
            TtsScriptVi = "Bạn đang ở gần Ốc Vũ. Điểm dễ nhớ của quán là vị me chua ngọt khá rõ, hợp với các món ốc xào. Nếu muốn gọi an toàn, hãy bắt đầu bằng ốc xào me hoặc nghêu hấp sả. Buổi tối khu này đông xe, nên bạn nên nhìn kỹ biển quán trước khi băng qua đường.",
            TtsScriptEn = "You are approaching Oc Vu, a popular seafood restaurant known for its wide variety of snail dishes and its signature sweet and sour tamarind dipping sauce. The spacious setting, friendly service, and affordable prices attract many diners, especially groups of friends and families in the evening.",
            TtsScriptZh = "您正在接近Ốc Vũ餐厅。这是一家以丰富多样的螺类菜品和独特的罗望子酸甜蘸酱而闻名的餐厅。宽敞的环境、热情的服务以及合理的价格，使这里在晚上经常座无虚席。",
            TtsScriptJa = "あなたはオック・ヴーに近づいています。この店は豊富な貝料理と甘酸っぱいタマリンドソースで有名です。広い店内と親切なサービス、手頃な価格で、特に夜には多くの友人グループや家族連れで賑わいます。",
            TtsScriptDe = "Sie nähern sich dem Restaurant Oc Vu. Das Lokal ist bekannt für seine große Auswahl an Schneckengerichten und seine charakteristische süß-saure Tamarindensauce. Der großzügige Raum, der freundliche Service und die günstigen Preise machen es besonders abends zu einem beliebten Treffpunkt.",
            IsActive = true,
            ImageUrl = "ocvu.jpg",
            QrCode = "poi:oc-vu"
        },
        new SeedPoiData
        {
            Name = "Ốc Thảo Quận 4",
            Address = "383 Đ. Vĩnh Khánh, Quận 4, TP.HCM",
            Latitude = 10.7616799,
            Longitude = 106.7023636,
            RadiusMeters = 20,
            Priority = 8,
            Description = "Nên gọi: nghêu hấp sả, ốc móng tay xào rau muống, sò lông nướng. Giá tham khảo: 55.000đ - 95.000đ/món. Đông nhất: 19:00 - 22:00. Điểm khác biệt: cảm giác quán địa phương, món quen, dễ chọn cho bữa ăn khuya.",
            TtsScriptVi = "Bạn đang ở gần Ốc Thảo Quận 4. Quán phù hợp khi bạn muốn ăn những món ốc quen, không cần chọn quá cầu kỳ. Nghêu hấp sả và ốc móng tay xào rau muống là hai món dễ gọi. Nếu đang theo tour, đây là điểm dừng tốt để nghỉ chân trước khi đi tiếp.",
            TtsScriptEn = "You are approaching Oc Thao, a well-known seafood spot among local residents. The restaurant offers a variety of fresh seafood dishes prepared with rich Saigon flavors. Its open space and reasonable prices make it a comfortable place for dinner gatherings.",
            TtsScriptZh = "您正在接近Ốc Thảo餐厅。这是当地居民非常熟悉的一家海鲜餐厅。餐厅提供多种新鲜海鲜，并以浓郁的西贡风味烹制。",
            TtsScriptJa = "あなたはオック・タオに近づいています。この店は地元の人々にもよく知られているシーフード店です。新鮮な海鮮をサイゴン風の濃い味付けで楽しむことができます。",
            TtsScriptDe = "Sie nähern sich dem Restaurant Oc Thao. Dieses Lokal ist bei Einheimischen sehr beliebt und bietet eine große Auswahl an frischen Meeresfrüchten mit typisch saigonischem Geschmack.",
            IsActive = true,
            ImageUrl = "octhao.jpg",
            QrCode = "poi:oc-thao"
        },
        new SeedPoiData
        {
            Name = "Ớt Xiêm Quán",
            Address = "568 Đ. Vĩnh Khánh, Phường 10, Quận 4, Hồ Chí Minh, Việt Nam",
            Latitude = 10.7611663,
            Longitude = 106.7057009,
            RadiusMeters = 20,
            Priority = 8,
            Description = "Nên gọi: gà tiềm ớt xiêm, bao tử hầm tiêu, món lẩu nóng. Giá tham khảo: 90.000đ - 220.000đ/món. Đông nhất: giờ ăn tối. Điểm khác biệt: đổi vị khỏi các quán ốc, hợp nhóm muốn món nóng và no hơn.",
            TtsScriptVi = "Bạn đang ở gần Ớt Xiêm Quán. Nếu đã ăn nhiều món ốc, đây là điểm đổi vị khá hợp lý với gà tiềm ớt xiêm hoặc bao tử hầm tiêu. Món ở đây thiên về vị nóng và no, hợp đi cùng nhóm sau một vòng dạo phố Vĩnh Khánh.",
            TtsScriptEn = "You are approaching Ot Xiem Quan, a popular dining spot on Vinh Khanh Street. The restaurant stands out with its lively atmosphere and flavorful dishes. It is a great place for casual gatherings, dinner outings, and experiencing the vibrant food culture of District 4.",
            TtsScriptZh = "您正在接近Ớt Xiêm Quán餐厅。这是永庆街上一家颇受欢迎的餐厅，以热闹的氛围和浓郁的菜肴风味而闻名。这里很适合朋友聚餐和体验第四郡的美食文化。",
            TtsScriptJa = "あなたはỚt Xiêm Quánに近づいています。この店はヴィンカイン通りで人気のある飲食店で、にぎやかな雰囲気と味わい深い料理が特徴です。友人との食事や第4区のグルメ文化を楽しむのにぴったりの場所です。",
            TtsScriptDe = "Sie nähern sich dem Restaurant Ớt Xiêm Quán. Dieses Lokal an der Vinh-Khanh-Straße ist für seine lebhafte Atmosphäre und seine kräftig gewürzten Gerichte bekannt. Es eignet sich gut für gesellige Abende und um die Esskultur von Distrikt 4 zu erleben.",
            IsActive = true,
            ImageUrl = "otxiem.jpg",
            QrCode = "poi:ot-xiem-quan"
        },
        new SeedPoiData
        {
            Name = "Ốc Đêm Vĩnh Khánh",
            Address = "474 Đ. Vĩnh Khánh, Quận 4, TP.HCM",
            Latitude = 10.7605012,
            Longitude = 106.7041323,
            RadiusMeters = 18,
            Priority = 6,
            Description = "Nên gọi: ốc luộc, sò nướng, nghêu hấp. Giá tham khảo: 50.000đ - 90.000đ/món. Đông nhất: 21:00 trở đi. Điểm khác biệt: hợp ăn khuya, gọi món nhanh, không cần bữa quá nặng.",
            TtsScriptVi = "Bạn đang ở gần Ốc Đêm Vĩnh Khánh. Quán hợp nhất khi bạn muốn ăn khuya hoặc chỉ gọi vài món nhẹ. Ốc luộc, sò nướng và nghêu hấp là những lựa chọn dễ bắt đầu. Nếu đi muộn, hãy hỏi món còn tươi trong ngày trước khi gọi nhiều.",
            TtsScriptEn = "You are approaching Oc Dem Vinh Khanh. As its name suggests, this restaurant stays open late and becomes lively when the street lights come on. It is a great place to enjoy simple but fresh seafood dishes at night.",
            TtsScriptZh = "您正在接近Ốc Đêm Vĩnh Khánh餐厅。正如名字所示，这家餐厅营业到深夜，当街道灯光亮起时这里非常热闹。",
            TtsScriptJa = "あなたはオック・デム・ヴィンカインに近づいています。名前の通り、この店は深夜まで営業しており、夜になるととても賑やかになります。",
            TtsScriptDe = "Sie nähern sich dem Restaurant Oc Dem Vinh Khanh. Wie der Name schon sagt, ist das Lokal bis spät in die Nacht geöffnet und wird besonders lebhaft, wenn die Straße am Abend belebt ist.",
            IsActive = true,
            ImageUrl = "ocdemvinhkhanh.jpg",
            QrCode = "poi:oc-dem-vinh-khanh"
        },
        new SeedPoiData
        {
            Name = "Ốc Nhi",
            Address = "262 Đ. Vĩnh Khánh, Quận 4, TP.HCM",
            Latitude = 10.7612809,
            Longitude = 106.7059746,
            RadiusMeters = 16,
            Priority = 5,
            Description = "Nên gọi: ốc luộc, ốc xào bơ, trứng cút lộn xào me. Giá tham khảo: 30.000đ - 70.000đ/món. Đông nhất: đầu buổi tối. Điểm khác biệt: giá mềm, hợp sinh viên hoặc người muốn thử nhiều món nhỏ.",
            TtsScriptVi = "Bạn đang ở gần Ốc Nhi. Đây là lựa chọn hợp túi tiền nếu bạn muốn thử nhiều món nhỏ thay vì gọi một phần lớn. Ốc luộc và trứng cút lộn xào me thường dễ ăn. Quán nhỏ nên trải nghiệm sẽ gần với kiểu ăn vỉa hè Sài Gòn hơn là nhà hàng.",
            TtsScriptEn = "You are approaching Oc Nhi, a small but popular seafood stall known for its very affordable prices and tasty snail dishes. Although the menu is simple, the flavors represent the authentic street food style of Saigon. This place is especially popular among students who want to enjoy a variety of dishes without spending too much money.",
            TtsScriptZh = "您正在接近Ốc Nhi餐厅。这是一家小而受欢迎的海鲜小店，以价格实惠和味道可口而闻名。虽然菜单比较简单，但这里的菜肴展现了西贡街头美食的特色风味。",
            TtsScriptJa = "あなたはオック・ニに近づいています。この店は小さなシーフード店ですが、手頃な価格と美味しい貝料理で人気があります。シンプルなメニューですが、サイゴンの屋台料理らしい味わいを楽しむことができます。",
            TtsScriptDe = "Sie nähern sich dem Restaurant Oc Nhi. Es handelt sich um ein kleines, aber sehr beliebtes Seafood-Lokal mit besonders günstigen Preisen. Die Gerichte sind einfach, spiegeln jedoch den typischen Street-Food-Geschmack von Saigon wider.",
            IsActive = true,
            ImageUrl = "ocnhi.jpg",
            QrCode = "poi:oc-nhi"
        },
        new SeedPoiData
        {
            Name = "Ốc Loan",
            Address = "129 Đ. Vĩnh Khánh, Quận 4, TP.HCM",
            Latitude = 10.7612240,
            Longitude = 106.7026292,
            RadiusMeters = 16,
            Priority = 4,
            Description = "Nên gọi: mực nướng, nghêu hấp sả, ốc xào bơ. Giá tham khảo: 60.000đ - 110.000đ/món. Đông nhất: 18:30 - 21:00. Điểm khác biệt: vị dễ ăn, hợp nhóm bạn muốn ngồi nhanh và gọi món quen.",
            TtsScriptVi = "Bạn đang ở gần Ốc Loan. Nếu cần một điểm dừng dễ gọi món, mực nướng và nghêu hấp sả là lựa chọn khá an toàn. Quán hợp đi nhóm nhỏ, gọi vài món chia nhau và tiếp tục dạo khu Vĩnh Khánh sau đó.",
            TtsScriptEn = "You are approaching Oc Loan, a seafood restaurant with a youthful atmosphere and a variety of flavorful dishes such as butter stir-fried snails, steamed clams with lemongrass, and grilled squid. The friendly environment makes it a great place for friends to gather in the evening.",
            TtsScriptZh = "您正在接近Ốc Loan餐厅。这是一家充满年轻氛围的海鲜餐厅，提供多种美味菜肴，例如黄油炒螺、香茅蒸蛤蜊以及烤鱿鱼。",
            TtsScriptJa = "あなたはオック・ローンに近づいています。この店は若者に人気のシーフード店で、バター炒めの貝やレモングラス蒸しの貝、焼きイカなどの料理を楽しむことができます。",
            TtsScriptDe = "Sie nähern sich dem Restaurant Oc Loan. Dieses Seafood-Lokal hat eine junge und lebhafte Atmosphäre und bietet verschiedene Gerichte wie in Butter gebratene Schnecken, gedämpfte Muscheln mit Zitronengras und gegrillten Tintenfisch.",
            IsActive = true,
            ImageUrl = "ocloan.jpg",
            QrCode = "poi:oc-loan"
        },
        new SeedPoiData
        {
            Name = "Ốc Bụi",
            Address = "539 Đ. Vĩnh Khánh, Quận 4, TP.HCM",
            Latitude = 10.7605965,
            Longitude = 106.7039361,
            RadiusMeters = 16,
            Priority = 3,
            Description = "Nên gọi: ốc len xào dừa, sò huyết rang me, ốc mỡ xào tỏi. Giá tham khảo: 55.000đ - 95.000đ/món. Đông nhất: sau 19:00. Điểm khác biệt: kiểu quán nhỏ, gần đường phố, hợp người thích cảm giác ăn ốc bình dân.",
            TtsScriptVi = "Bạn đang ở gần Ốc Bụi. Điểm hay của quán là cảm giác rất đường phố: nhỏ, gần, dễ ghé. Nếu bạn thích vị béo, hãy thử ốc len xào dừa; nếu thích vị chua ngọt thì chọn sò huyết rang me.",
            TtsScriptEn = "You are approaching Oc Bui, a small seafood restaurant that represents the authentic taste of Saigon street food. Its simple setting close to the street allows visitors to enjoy their meal while experiencing the lively atmosphere of Vinh Khanh food street.",
            TtsScriptZh = "您正在接近Ốc Bụi餐厅。这是一家小型海鲜店，体现了西贡街头海鲜的特色风味。简朴的环境让您在用餐的同时，也能感受永庆美食街的热闹气氛。",
            TtsScriptJa = "あなたはオック・ブイに近づいています。この小さなシーフード店では、サイゴンらしい屋台の味を楽しむことができます。",
            TtsScriptDe = "Sie nähern sich dem Restaurant Oc Bui. Dieses kleine Seafood-Lokal vermittelt den typischen Geschmack der Saigoner Straßenküche.",
            IsActive = true,
            ImageUrl = "ocbui.jpg",
            QrCode = "poi:oc-bui"
        },
        new SeedPoiData
        {
            Name = "Ốc Ty",
            Address = "12 Đ. Vĩnh Khánh, Quận 4, TP.HCM",
            Latitude = 10.7607246,
            Longitude = 106.7069244,
            RadiusMeters = 15,
            Priority = 2,
            Description = "Nên gọi: ốc hấp, ốc xào tỏi, sò nướng mỡ hành. Giá tham khảo: 45.000đ - 85.000đ/món. Đông nhất: buổi tối. Điểm khác biệt: điểm dừng nhỏ gần đầu phố, hợp ghé nhanh trước khi vào khu đông hơn.",
            TtsScriptVi = "Bạn đang ở gần Ốc Ty. Đây là điểm ghé nhanh nếu bạn muốn ăn vài món ốc truyền thống trước khi đi sâu hơn vào phố Vĩnh Khánh. Món nên thử là ốc xào tỏi hoặc sò nướng mỡ hành. Không gian nhỏ nên hợp nhóm ít người hơn.",
            TtsScriptEn = "You are approaching Oc Ty, a small snail restaurant near the beginning of Vinh Khanh Street. The dishes here follow the traditional southern Vietnamese style. It is a convenient stop for visitors who want to try a few simple seafood dishes before continuing their food journey along the street.",
            TtsScriptZh = "您正在接近Ốc Ty餐厅。这是一家位于永庆街入口附近的小型海鲜店，提供传统南方风味的螺类料理。",
            TtsScriptJa = "あなたはオック・ティに近づいています。この店はヴィンカイン通りの入り口付近にある小さなシーフード店です。",
            TtsScriptDe = "Sie nähern sich dem Restaurant Oc Ty. Dieses kleine Lokal befindet sich am Anfang der Vinh-Khanh-Straße und bietet traditionelle Schneckengerichte im südvietnamesischen Stil.",
            IsActive = true,
            ImageUrl = "octy.jpg",
            QrCode = "poi:oc-ty"
        },
        new SeedPoiData
        {
            Name = "Sườn Muối Ớt Q4",
            Address = "712 Đ. Vĩnh Khánh, Quận 4, TP.HCM",
            Latitude = 10.7607418,
            Longitude = 106.7036081,
            RadiusMeters = 18,
            Priority = 1,
            Description = "Nên gọi: sườn muối ớt, ba chỉ nướng, rau ăn kèm. Giá tham khảo: 90.000đ - 180.000đ/món. Đông nhất: giờ ăn tối. Điểm khác biệt: không phải quán ốc, hợp đổi vị bằng món nướng nhiều năng lượng.",
            TtsScriptVi = "Bạn đang ở gần Sườn Muối Ớt Quận 4. Nếu trong nhóm có người không muốn ăn ốc, đây là lựa chọn đổi vị khá rõ ràng. Món chính nên gọi là sườn muối ớt, ăn cùng rau để đỡ ngấy. Điểm này hợp làm bữa chính hơn là món ăn vặt.",
            TtsScriptEn = "You are approaching Suon Muoi Ot District 4, a restaurant famous for its grilled ribs with chili salt sauce. Unlike the seafood stalls nearby, this place offers Vietnamese-style barbecue with rich and smoky flavors.",
            TtsScriptZh = "您正在接近四郡的“Sườn Muối Ớt”餐厅。这家餐厅以辣椒盐烤排骨而闻名。",
            TtsScriptJa = "あなたはスオン・ムオイ・オット・クアン4に近づいています。この店はベトナム風バーベキューの代表的な料理であるチリソルト味の焼きスペアリブで有名です。",
            TtsScriptDe = "Sie nähern sich dem Restaurant Suon Muoi Ot im Bezirk 4. Dieses Lokal ist bekannt für seine gegrillten Rippchen mit Chili-Salz-Sauce.",
            IsActive = true,
            ImageUrl = "suonmuoiot.png",
            QrCode = "poi:suon-muoi-ot-q4"
        }
    ];
}

public sealed class SeedPoiData
{
    public string Name { get; init; } = string.Empty;
    public string Address { get; init; } = string.Empty;
    public string PhoneNumber { get; init; } = string.Empty;
    public double Latitude { get; init; }
    public double Longitude { get; init; }
    public double RadiusMeters { get; init; } = 25;
    public int Priority { get; init; } = 1;
    public string Description { get; init; } = string.Empty;
    public string TtsScriptVi { get; init; } = string.Empty;
    public string TtsScriptEn { get; init; } = string.Empty;
    public string TtsScriptZh { get; init; } = string.Empty;
    public string TtsScriptJa { get; init; } = string.Empty;
    public string TtsScriptDe { get; init; } = string.Empty;
    public bool IsActive { get; init; } = true;
    public string ImageUrl { get; init; } = string.Empty;
    public string QrCode { get; init; } = string.Empty;
}
