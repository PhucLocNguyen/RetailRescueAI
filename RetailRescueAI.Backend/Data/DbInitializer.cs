using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using RetailRescueAI.Backend.Models;

namespace RetailRescueAI.Backend.Data;

public static class DbInitializer
{
    public static string HashPassword(string password)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(password);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }

    public static async Task InitializeAsync(AppDbContext context)
    {
        await context.Database.EnsureCreatedAsync();

        if (await context.Users.AnyAsync())
        {
            return; // DB already seeded
        }

        var now = DateTime.UtcNow;

        // 1. Stores
        var store = new Store
        {
            StoreCode = "STORE-001",
            Name = "ライフマート 新宿東口店 (LifeMart Shinjuku)",
            Address = "東京都新宿区西新宿1-1-1",
            Phone = "03-1234-5678",
            CreatedAt = now.AddDays(-60)
        };
        context.Stores.Add(store);
        await context.SaveChangesAsync();

        // 2. Users
        var users = new List<User>
        {
            new()
            {
                Username = "staff",
                PasswordHash = HashPassword("staff123"),
                FullName = "山田 太郎 (Yamada Staff)",
                Role = "STAFF",
                StoreId = store.Id,
                CreatedAt = now.AddDays(-30)
            },
            new()
            {
                Username = "manager",
                PasswordHash = HashPassword("manager123"),
                FullName = "佐藤 店長 (Sato Manager)",
                Role = "MANAGER",
                StoreId = store.Id,
                CreatedAt = now.AddDays(-30)
            }
        };
        context.Users.AddRange(users);

        // 3. Categories
        var catBento = new ProductCategory { Name = "お弁当 (Bento)", Description = "日替わり弁当・丼もの" };
        var catSalad = new ProductCategory { Name = "サラダ・総菜 (Salad & Delica)", Description = "フレッシュサラダ・惣菜" };
        var catSand = new ProductCategory { Name = "サンドイッチ・パン (Bakery)", Description = "サンドイッチ・調理パン" };
        var catDrink = new ProductCategory { Name = "飲料・乳製品 (Drinks & Dairy)", Description = "お茶・ジュース・牛乳" };
        context.ProductCategories.AddRange(catBento, catSalad, catSand, catDrink);
        await context.SaveChangesAsync();

        // 4. Products
        var pBento1 = new Product
        {
            ProductCode = "BENTO-001",
            Name = "チキン南蛮弁当 (Chicken Bento)",
            Description = "ジューシーな鶏肉と特製タルタルソースの定番弁当",
            CategoryId = catBento.Id,
            Price = 550m,
            CostPrice = 320m,
            MinMarginPercent = 15.00m,
            MaxDiscountPercent = 50.00m,
            Barcode = "4901234567011",
            ImageUrl = "/images/products/chicken-bento.jpg",
            CreatedAt = now.AddDays(-30)
        };

        var pSalad1 = new Product
        {
            ProductCode = "SALAD-001",
            Name = "彩りサーモンサラダ (Salmon Salad)",
            Description = "スモークサーモンと新鮮グリーン野菜の和風サラダ",
            CategoryId = catSalad.Id,
            Price = 240m,
            CostPrice = 130m,
            MinMarginPercent = 15.00m,
            MaxDiscountPercent = 40.00m,
            Barcode = "4901234567028",
            ImageUrl = "/images/products/salmon-salad.jpg",
            CreatedAt = now.AddDays(-30)
        };

        var pSand1 = new Product
        {
            ProductCode = "SAND-001",
            Name = "こだわりたまごサンド (Egg Sandwich)",
            Description = "濃厚たまごフィリングをたっぷり挟んだサンドイッチ",
            CategoryId = catSand.Id,
            Price = 280m,
            CostPrice = 150m,
            MinMarginPercent = 15.00m,
            MaxDiscountPercent = 50.00m,
            Barcode = "4901234567035",
            ImageUrl = "/images/products/egg-sandwich.jpg",
            CreatedAt = now.AddDays(-30)
        };

        var pDrink1 = new Product
        {
            ProductCode = "DRINK-001",
            Name = "宇治緑茶 500ml (Uji Green Tea)",
            Description = "香り高い宇治抹茶仕立てのペットボトル緑茶",
            CategoryId = catDrink.Id,
            Price = 140m,
            CostPrice = 75m,
            MinMarginPercent = 20.00m,
            MaxDiscountPercent = 30.00m,
            Barcode = "4901234567042",
            ImageUrl = "/images/products/green-tea.jpg",
            CreatedAt = now.AddDays(-30)
        };

        var pBento2 = new Product
        {
            ProductCode = "BENTO-002",
            Name = "特選 ロースかつ丼 (Pork Cutlet Rice Bowl)",
            Description = "三元豚ロースをふんわり卵でとじた人気のカツ丼",
            CategoryId = catBento.Id,
            Price = 620m,
            CostPrice = 360m,
            MinMarginPercent = 15.00m,
            MaxDiscountPercent = 50.00m,
            Barcode = "4901234567059",
            ImageUrl = "/images/products/katsudon.jpg",
            CreatedAt = now.AddDays(-30)
        };

        context.Products.AddRange(pBento1, pSalad1, pSand1, pDrink1, pBento2);
        await context.SaveChangesAsync();

        // 5. Inventory Batches
        // Chicken Bento: Batch 1 (Critical: 30 units, expires in 18 hours), Batch 2 (Available: 40 units, expires in 42 hours)
        var bBento1 = new InventoryBatch
        {
            BatchCode = "BATCH-BENTO-001",
            ProductId = pBento1.Id,
            StoreId = store.Id,
            InitialQuantity = 40,
            RemainingQuantity = 30, // 30 remaining! (Matches core scenario)
            ProductionDate = now.AddHours(-12),
            ExpiryDate = now.AddHours(18), // 18 hours until expiry
            Status = "CRITICAL",
            CreatedAt = now.AddHours(-12)
        };

        var bBento2 = new InventoryBatch
        {
            BatchCode = "BATCH-BENTO-002",
            ProductId = pBento1.Id,
            StoreId = store.Id,
            InitialQuantity = 50,
            RemainingQuantity = 40,
            ProductionDate = now.AddHours(-2),
            ExpiryDate = now.AddHours(42),
            Status = "AVAILABLE",
            CreatedAt = now.AddHours(-2)
        };

        // Salmon Salad: Batch 1 (At-Risk: 25 units, expires in 26 hours)
        var bSalad1 = new InventoryBatch
        {
            BatchCode = "BATCH-SALAD-001",
            ProductId = pSalad1.Id,
            StoreId = store.Id,
            InitialQuantity = 30,
            RemainingQuantity = 25,
            ProductionDate = now.AddHours(-10),
            ExpiryDate = now.AddHours(26),
            Status = "AT_RISK",
            CreatedAt = now.AddHours(-10)
        };

        // Egg Sandwich: Batch 1 (Critical: 12 units, expires in 8 hours)
        var bSand1 = new InventoryBatch
        {
            BatchCode = "BATCH-SAND-001",
            ProductId = pSand1.Id,
            StoreId = store.Id,
            InitialQuantity = 20,
            RemainingQuantity = 12,
            ProductionDate = now.AddHours(-16),
            ExpiryDate = now.AddHours(8),
            Status = "CRITICAL",
            CreatedAt = now.AddHours(-16)
        };

        // Green Tea: Batch 1 (Available: 60 units, expires in 30 days)
        var bDrink1 = new InventoryBatch
        {
            BatchCode = "BATCH-DRINK-001",
            ProductId = pDrink1.Id,
            StoreId = store.Id,
            InitialQuantity = 80,
            RemainingQuantity = 60,
            ProductionDate = now.AddDays(-10),
            ExpiryDate = now.AddDays(30),
            Status = "AVAILABLE",
            CreatedAt = now.AddDays(-10)
        };

        // Katsudon: Batch 1 (At-Risk: 18 units, expires in 14 hours)
        var bBento3 = new InventoryBatch
        {
            BatchCode = "BATCH-BENTO-003",
            ProductId = pBento2.Id,
            StoreId = store.Id,
            InitialQuantity = 25,
            RemainingQuantity = 18,
            ProductionDate = now.AddHours(-10),
            ExpiryDate = now.AddHours(14),
            Status = "AT_RISK",
            CreatedAt = now.AddHours(-10)
        };

        context.InventoryBatches.AddRange(bBento1, bBento2, bSalad1, bSand1, bDrink1, bBento3);
        await context.SaveChangesAsync();

        // 6. Customers
        var c1 = new Customer
        {
            CustomerCode = "C000123",
            Name = "田中 健一 (Tanaka Kenichi)",
            Phone = "090-1111-2222",
            Email = "tanaka@example.com",
            Points = 150,
            CreatedAt = now.AddDays(-60)
        };
        var c2 = new Customer
        {
            CustomerCode = "C000456",
            Name = "鈴木 美咲 (Suzuki Misaki)",
            Phone = "090-3333-4444",
            Email = "suzuki@example.com",
            Points = 320,
            CreatedAt = now.AddDays(-45)
        };
        context.Customers.AddRange(c1, c2);
        await context.SaveChangesAsync();

        // 7. Seed 7 days of historical sales transactions
        var random = new Random(42);
        for (int i = 7; i >= 1; i--)
        {
            var date = now.AddDays(-i);
            // 8 to 15 sales per day
            int dailyTxCount = random.Next(10, 16);
            for (int j = 0; j < dailyTxCount; j++)
            {
                var hour = random.Next(11, 21); // between 11:00 and 21:00 (peak around 17:00-19:00)
                var saleTime = new DateTime(date.Year, date.Month, date.Day, hour, random.Next(0, 59), random.Next(0, 59));
                
                var sale = new Sale
                {
                    TransactionNumber = $"TX-{saleTime:yyyyMMdd}-{j:D3}",
                    StoreId = store.Id,
                    StaffId = users[0].Id,
                    CustomerId = j % 3 == 0 ? c1.Id : (j % 4 == 0 ? c2.Id : null),
                    PaymentMethod = j % 2 == 0 ? "CASH" : (j % 3 == 0 ? "QR_CODE" : "CARD"),
                    CreatedAt = saleTime,
                    Status = "COMPLETED"
                };

                // Add Bento
                var bentoQty = random.Next(1, 2);
                var bentoItem = new SaleItem
                {
                    Sale = sale,
                    ProductId = pBento1.Id,
                    Quantity = bentoQty,
                    UnitPrice = pBento1.Price,
                    DiscountAmount = 0m,
                    FinalPrice = pBento1.Price * bentoQty
                };
                sale.Items.Add(bentoItem);

                // Sometimes add salad or drink
                if (random.Next(0, 2) == 1)
                {
                    var drinkItem = new SaleItem
                    {
                        Sale = sale,
                        ProductId = pDrink1.Id,
                        Quantity = 1,
                        UnitPrice = pDrink1.Price,
                        DiscountAmount = 0m,
                        FinalPrice = pDrink1.Price
                    };
                    sale.Items.Add(drinkItem);
                }

                sale.Subtotal = sale.Items.Sum(x => x.FinalPrice);
                sale.TotalAmount = sale.Subtotal;
                context.Sales.Add(sale);

                // Add Customer purchase history
                if (sale.CustomerId.HasValue)
                {
                    foreach (var item in sale.Items)
                    {
                        context.CustomerPurchaseHistories.Add(new CustomerPurchaseHistory
                        {
                            CustomerId = sale.CustomerId.Value,
                            ProductId = item.ProductId,
                            Quantity = item.Quantity,
                            UnitPrice = item.UnitPrice,
                            PurchaseDate = saleTime
                        });
                    }
                }
            }
        }
        await context.SaveChangesAsync();

        // 8. Seed a previous completed Promotion Result for baseline analytics
        var prevPromo = new Promotion
        {
            PromotionCode = "PROMO-PREV-001",
            Name = "【先週実績】チキン南蛮弁当 夕方20%OFF",
            PromotionType = "DIRECT_DISCOUNT",
            Status = "APPROVED",
            TargetProductId = pBento1.Id,
            DiscountPercent = 20.00m,
            StartTime = now.AddDays(-3).Date.AddHours(17),
            EndTime = now.AddDays(-3).Date.AddHours(22),
            CreatedVia = "AI_AGENT",
            CreatedBy = "OrchestratorAgent",
            ApprovedBy = "佐藤 店長",
            ApprovedAt = now.AddDays(-3).Date.AddHours(16),
            AiReasoning = "賞味期限迫るチキン弁当の廃棄抑制のため夕方20%割引を提案",
            CreatedAt = now.AddDays(-3).Date.AddHours(15)
        };
        context.Promotions.Add(prevPromo);
        await context.SaveChangesAsync();

        var prevResult = new PromotionResult
        {
            PromotionId = prevPromo.Id,
            ProductId = pBento1.Id,
            BatchId = bBento1.Id,
            InitialStock = 30,
            StockBeforePromotion = 30,
            UnitsSold = 26,
            UnitsRemaining = 4,
            ExpiredUnits = 4,
            ExpectedSales = 24,
            ActualSales = 26,
            ExpectedWasteAvoided = 15,
            ActualWasteAvoided = 17,
            WasteReductionRate = 81.00m, // 81% waste reduction!
            ExpectedRevenue = 10560m,
            ActualRevenue = 11440m,
            EvaluatedAt = now.AddDays(-2)
        };
        context.PromotionResults.Add(prevResult);
        await context.SaveChangesAsync();
    }
}
