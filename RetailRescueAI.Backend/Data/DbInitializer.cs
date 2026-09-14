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

        var now = RetailRescueAI.Backend.Common.AppClock.Now;
        bool needReset = false;

        if (!await context.Users.AnyAsync())
        {
            needReset = true;
        }
        else
        {
            // Ensure we always have active demo batches ready: at least 2 critical batches (expiring in 2-8h)
            var eightHoursLater = now.AddHours(8);
            var criticalBatchesCount = await context.InventoryBatches.CountAsync(b => b.ExpiryDate > now && b.ExpiryDate <= eightHoursLater && b.RemainingQuantity > 0);
            if (criticalBatchesCount < 2)
            {
                needReset = true;
            }
        }

        if (needReset)
        {
            await ResetDemoDataAsync(context);
        }
    }

    public static async Task ResetDemoDataAsync(AppDbContext context)
    {
        // 1. Clean existing records in reverse dependency order
        context.PromotionResults.RemoveRange(context.PromotionResults);
        context.AIRecommendationEvidences.RemoveRange(context.AIRecommendationEvidences);
        context.AIRecommendations.RemoveRange(context.AIRecommendations);
        context.SaleItems.RemoveRange(context.SaleItems);
        context.Sales.RemoveRange(context.Sales);
        context.PromotionConditions.RemoveRange(context.PromotionConditions);
        context.PromotionProducts.RemoveRange(context.PromotionProducts);
        context.Promotions.RemoveRange(context.Promotions);
        context.CustomerPurchaseHistories.RemoveRange(context.CustomerPurchaseHistories);
        context.Customers.RemoveRange(context.Customers);
        context.InventoryBatches.RemoveRange(context.InventoryBatches);
        context.Products.RemoveRange(context.Products);
        context.ProductCategories.RemoveRange(context.ProductCategories);
        context.Users.RemoveRange(context.Users);
        context.Stores.RemoveRange(context.Stores);
        await context.SaveChangesAsync();

        var now = RetailRescueAI.Backend.Common.AppClock.Now;

        // 2. Stores
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

        // 3. Users
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

        // 4. Categories
        var catBento = new ProductCategory { Name = "お弁当 (Bento)", Description = "日替わり弁当・丼もの" };
        var catSalad = new ProductCategory { Name = "サラダ・総菜 (Salad & Delica)", Description = "フレッシュサラダ・惣菜" };
        var catSand = new ProductCategory { Name = "サンドイッチ・パン (Bakery)", Description = "サンドイッチ・調理パン" };
        var catDrink = new ProductCategory { Name = "飲料・乳製品 (Drinks & Dairy)", Description = "お茶・ジュース・牛乳" };
        context.ProductCategories.AddRange(catBento, catSalad, catSand, catDrink);
        await context.SaveChangesAsync();

        // 5. Products
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

        // 6. Inventory Batches with fresh, realistic shelf-lives relative to NOW (Japan Standard Time JST)
        // A. Critical Bento: 28 remaining, expires in 2.5 hours (Prime for evening peak 30% discount!)
        var bBento1 = new InventoryBatch
        {
            BatchCode = "BATCH-BENTO-001",
            ProductId = pBento1.Id,
            StoreId = store.Id,
            InitialQuantity = 40,
            RemainingQuantity = 28,
            ProductionDate = now.AddHours(-18),
            ExpiryDate = now.AddHours(2.5),
            Status = "CRITICAL",
            CreatedAt = now.AddHours(-18)
        };

        // B. Critical Egg Sandwich: 16 remaining, expires in 1.5 hours (Perfect for Chatbot 30% / 3割 discount demo!)
        var bSand1 = new InventoryBatch
        {
            BatchCode = "BATCH-SAND-001",
            ProductId = pSand1.Id,
            StoreId = store.Id,
            InitialQuantity = 25,
            RemainingQuantity = 16,
            ProductionDate = now.AddHours(-18),
            ExpiryDate = now.AddHours(1.5),
            Status = "CRITICAL",
            CreatedAt = now.AddHours(-18)
        };

        // C. At-Risk Salad: 22 remaining, expires in 16 hours
        var bSalad1 = new InventoryBatch
        {
            BatchCode = "BATCH-SALAD-001",
            ProductId = pSalad1.Id,
            StoreId = store.Id,
            InitialQuantity = 30,
            RemainingQuantity = 22,
            ProductionDate = now.AddHours(-8),
            ExpiryDate = now.AddHours(16.0),
            Status = "AT_RISK",
            CreatedAt = now.AddHours(-8)
        };

        // D. At-Risk Katsudon: 15 remaining, expires in 14 hours
        var bBento3 = new InventoryBatch
        {
            BatchCode = "BATCH-BENTO-003",
            ProductId = pBento2.Id,
            StoreId = store.Id,
            InitialQuantity = 25,
            RemainingQuantity = 15,
            ProductionDate = now.AddHours(-10),
            ExpiryDate = now.AddHours(14.0),
            Status = "AT_RISK",
            CreatedAt = now.AddHours(-10)
        };

        // E. Fresh Bento: 45 remaining, expires in 44 hours
        var bBento2 = new InventoryBatch
        {
            BatchCode = "BATCH-BENTO-002",
            ProductId = pBento1.Id,
            StoreId = store.Id,
            InitialQuantity = 50,
            RemainingQuantity = 45,
            ProductionDate = now.AddHours(-4),
            ExpiryDate = now.AddHours(44.0),
            Status = "AVAILABLE",
            CreatedAt = now.AddHours(-4)
        };

        // F. Fresh Egg Sandwich: 25 remaining, expires in 32 hours
        var bSand2 = new InventoryBatch
        {
            BatchCode = "BATCH-SAND-002",
            ProductId = pSand1.Id,
            StoreId = store.Id,
            InitialQuantity = 30,
            RemainingQuantity = 25,
            ProductionDate = now.AddHours(-4),
            ExpiryDate = now.AddHours(32.0),
            Status = "AVAILABLE",
            CreatedAt = now.AddHours(-4)
        };

        // G. Stable Beverage (Uji Green Tea): 75 remaining, expires in 45 days (Companion drink for meal combos!)
        var bDrink1 = new InventoryBatch
        {
            BatchCode = "BATCH-DRINK-001",
            ProductId = pDrink1.Id,
            StoreId = store.Id,
            InitialQuantity = 80,
            RemainingQuantity = 75,
            ProductionDate = now.AddDays(-10),
            ExpiryDate = now.AddDays(45),
            Status = "AVAILABLE",
            CreatedAt = now.AddDays(-10)
        };

        // H. Expired Batch (Egg Sandwich): 4 units remaining, expired 1.5 hours ago (Demonstrates food safety rule BR-007)
        var bSandExpired = new InventoryBatch
        {
            BatchCode = "BATCH-SAND-EXPIRED",
            ProductId = pSand1.Id,
            StoreId = store.Id,
            InitialQuantity = 10,
            RemainingQuantity = 4,
            ProductionDate = now.AddHours(-26),
            ExpiryDate = now.AddHours(-1.5),
            Status = "EXPIRED",
            CreatedAt = now.AddHours(-26)
        };

        context.InventoryBatches.AddRange(bBento1, bSand1, bSalad1, bBento3, bBento2, bSand2, bDrink1, bSandExpired);
        await context.SaveChangesAsync();

        // 7. Customers
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

        // 8. 7 days of historical sales transactions leading up to NOW
        var random = new Random(42);
        for (int i = 7; i >= 1; i--)
        {
            var date = now.AddDays(-i);
            int dailyTxCount = random.Next(11, 16);
            for (int j = 0; j < dailyTxCount; j++)
            {
                var hour = random.Next(11, 21);
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

                // Sometimes add sandwich or salad or drink
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

                if (random.Next(0, 3) == 1)
                {
                    var sandItem = new SaleItem
                    {
                        Sale = sale,
                        ProductId = pSand1.Id,
                        Quantity = 1,
                        UnitPrice = pSand1.Price,
                        DiscountAmount = 0m,
                        FinalPrice = pSand1.Price
                    };
                    sale.Items.Add(sandItem);
                }

                sale.Subtotal = sale.Items.Sum(x => x.FinalPrice);
                sale.TotalAmount = sale.Subtotal;
                context.Sales.Add(sale);

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

        // 9. Seed a previous completed Promotion Result for baseline analytics & charts
        var prevPromo = new Promotion
        {
            PromotionCode = "PROMO-PREV-001",
            Name = "【先週実績】チキン南蛮弁当 夕方20%OFF",
            PromotionType = "DIRECT_DISCOUNT",
            Status = "APPROVED",
            TargetProductId = pBento1.Id,
            TargetBatchId = bBento1.Id,
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
            WasteReductionRate = 81.00m,
            ExpectedRevenue = 10560m,
            ActualRevenue = 11440m,
            EvaluatedAt = now.AddDays(-2)
        };
        context.PromotionResults.Add(prevResult);

        await context.SaveChangesAsync();
    }
}
