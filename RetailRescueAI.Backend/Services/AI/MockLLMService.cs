using System.Text.RegularExpressions;
using RetailRescueAI.Backend.DTOs;

namespace RetailRescueAI.Backend.Services.AI;

public class MockLLMService : ILLMService
{
    private readonly ILogger<MockLLMService> _logger;

    public MockLLMService(ILogger<MockLLMService> logger)
    {
        _logger = logger;
    }

    public bool IsConfigured => true;

    public Task<string> GenerateTextAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[MockLLMService] Generating deterministic reasoning in Japanese...");

        // Return contextual Japanese reasoning based on keywords in prompt
        if (userPrompt.Contains("チキン弁当") || userPrompt.Contains("BENTO-001"))
        {
            return Task.FromResult(@"【AI分析サマリー】
対象商品：チキン南蛮弁当（BATCH-BENTO-001）
・現在庫数：30個
・賞味期限まで：残り18時間
・日販平均：12個/日
・通常予測販売数：約9個
・潜在的廃棄リスク：約21個（廃棄率 70%見込み）

【推奨アクション】
推奨施策：20% OFF（直前割）
推奨実施時間帯：本日 17:00 〜 22:00（ピーク夕方時間帯）
期待販売数：24〜28個
期待廃棄削減率：約80%〜85%
期待回収収益：約 ¥10,500

【AIによる根拠・証拠】
過去の販売実績において、夕方ピーク（17:00〜20:00）に20%割引を適用した際、売上数が通常の2.2倍に跳ね上がった実績があります。原価率58%（¥320/¥550）に対し、20%割引後（¥440）でも粗利益¥120（粗利率27%）を維持できるため、店舗利益基準（最低利益率15%）を十分にクリアします。");
        }

        return Task.FromResult(@"【AI分析サマリー】
対象商品：賞味期限切迫商品
・在庫数および賞味期限の残存時間から、通常販売ペースでは期限内完売が困難と判定。
・早期の価格訴求プロモーションにより、廃棄ロスの最小化と売上回収を推奨します。
・粗利益基準および期限ルール（BR-003）をクリアした条件を設定しました。");
    }

    public Task<string> ChatAsync(string systemPrompt, List<ChatMessageDto> history, string userMessage, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[MockLLMService] Answering manager chat in Japanese dynamically...");

        var msg = userMessage.ToLower();

        // Extract discount percent requested by user if present (e.g., 30%, 25%, 3割)
        decimal discount = 20m;
        var match = Regex.Match(userMessage, @"(\d{1,2})\s*(?:%|％)");
        if (match.Success && decimal.TryParse(match.Groups[1].Value, out var parsedPct))
        {
            discount = Math.Clamp(parsedPct, 5, 70);
        }
        else if (userMessage.Contains("3割") || userMessage.Contains("30"))
        {
            discount = 30m;
        }
        else if (userMessage.Contains("半額") || userMessage.Contains("50"))
        {
            discount = 50m;
        }

        if (msg.Contains("サンド") || msg.Contains("たまご") || msg.Contains("sand") || msg.Contains("egg"))
        {
            decimal price = 280m;
            decimal discounted = Math.Round(price * (1 - discount / 100m), 0);
            return Task.FromResult($@"こだわりたまごサンド（ロット: BATCH-SAND-001）の在庫状況を確認しました。

現在庫：**12個**
賞味期限まで：**残り約6時間（CRITICAL - 切迫）**
定価：¥{price:N0}（原価：¥150）

日販平均ペースでは期限内に完売せず、約10個が廃棄となる危険があります。

**【AIの提案】**
本日 **17:00 〜 22:00** に **{discount:F0}% OFF（割引後 ¥{discounted:N0}）** の直前割プロモーションを推奨します。
値引き後でも粗利益 ¥{discounted - 150:N0}（粗利率 {((discounted - 150) / discounted * 100):F1}%）を確保し、最低粗利率基準（15%）を十分に維持できます。

以下のプランを「承認待ち（PENDING）」として登録しますか？

```json:proposal
{{
  ""batchCode"": ""BATCH-SAND-001"",
  ""promotionType"": ""DIRECT_DISCOUNT"",
  ""discountPercent"": {discount:F0},
  ""name"": ""こだわりたまごサンド 夕方直前割 {discount:F0}% OFF"",
  ""reasoning"": ""残り12個（期限まで6h）の廃棄回避のため、{discount:F0}%割引（¥{discounted:N0}）で早期完売を促します。""
}}
```");
        }

        if (msg.Contains("かつ丼") || msg.Contains("ロース") || msg.Contains("pork") || msg.Contains("cutlet"))
        {
            decimal price = 620m;
            decimal discounted = Math.Round(price * (1 - discount / 100m), 0);
            return Task.FromResult($@"特選 ロースかつ丼（ロット: BATCH-BENTO-003）の在庫状況を確認しました。

現在庫：**18個**
賞味期限まで：**残り約14時間（AT_RISK）**
定価：¥{price:N0}（原価：¥380）

**【AIの提案】**
夕方ピーク帯（17:00〜22:00）に **{discount:F0}% OFF（割引後 ¥{discounted:N0}）** のプロモーションを推奨します。
値引き後でも粗利益 ¥{discounted - 380:N0} を維持し、安全基準を満たしています。

```json:proposal
{{
  ""batchCode"": ""BATCH-BENTO-003"",
  ""promotionType"": ""DIRECT_DISCOUNT"",
  ""discountPercent"": {discount:F0},
  ""name"": ""特選 ロースかつ丼 夕方割 {discount:F0}% OFF"",
  ""reasoning"": ""残り18個の消化促進のため、夕方ピークに{discount:F0}%割引（¥{discounted:N0}）を推奨。""
}}
```");
        }

        if (msg.Contains("サラダ") || msg.Contains("サーモン") || msg.Contains("salad"))
        {
            decimal price = 240m;
            decimal discounted = Math.Round(price * (1 - discount / 100m), 0);
            return Task.FromResult($@"彩りサーモンサラダ（ロット: BATCH-SALAD-001）の在庫状況を確認しました。

現在庫：**25個**
賞味期限まで：**残り約26時間（MEDIUM）**
定価：¥{price:N0}（原価：¥130）

**【AIの提案】**
ランチおよび夕方ピークに向け、**{discount:F0}% OFF（割引後 ¥{discounted:N0}）** または弁当とのセット販売を推奨します。

```json:proposal
{{
  ""batchCode"": ""BATCH-SALAD-001"",
  ""promotionType"": ""DIRECT_DISCOUNT"",
  ""discountPercent"": {discount:F0},
  ""name"": ""彩りサーモンサラダ タイムセール {discount:F0}% OFF"",
  ""reasoning"": ""現在庫25個の消化を早めるため、{discount:F0}%割引（¥{discounted:N0}）を提案。""
}}
```");
        }

        if (msg.Contains("チキン") || msg.Contains("弁当") || msg.Contains("bento"))
        {
            decimal price = 550m;
            decimal discounted = Math.Round(price * (1 - discount / 100m), 0);
            return Task.FromResult($@"チキン南蛮弁当（ロット: BATCH-BENTO-001）の在庫状況を確認しました。

現在庫：**30個**
賞味期限まで：**残り約18時間（AT_RISK）**
定価：¥{price:N0}（原価：¥320）

通常の販売ペース（平均12個/日）のままだと、約21個が売れ残り廃棄になるリスクがあります。

**【AIの提案】**
本日 **17:00 〜 22:00** の夕方ピーク帯に **{discount:F0}% OFF（割引後 ¥{discounted:N0}）** のプロモーションを推奨します。
粗利益¥{discounted - 320:N0}（粗利率 {((discounted - 320) / discounted * 100):F1}%）が確保され、最低利益率基準（15%）をクリアしています。

```json:proposal
{{
  ""batchCode"": ""BATCH-BENTO-001"",
  ""promotionType"": ""DIRECT_DISCOUNT"",
  ""discountPercent"": {discount:F0},
  ""name"": ""チキン南蛮弁当 夕方直前割 {discount:F0}% OFF"",
  ""reasoning"": ""残り30個の早期完売に向け、17:00〜22:00の夕方ピーク帯に{discount:F0}%OFF（¥{discounted:N0}）のプロモーションを推奨します。""
}}
```");
        }

        if (msg.Contains("割引") || msg.Contains("プロモーション") || msg.Contains("提案") || msg.Contains("危険") || msg.Contains("切迫") || msg.Contains("賞味期限"))
        {
            return Task.FromResult($@"店長、本日の危険在庫（CRITICAL / AT_RISK）の状況です：

1. 🔴 **こだわりたまごサンド（BATCH-SAND-001）**：残12個、賞味期限まで残り約6時間（最優先対策）
2. 🟠 **特選 ロースかつ丼（BATCH-BENTO-003）**：残18個、賞味期限まで残り約14時間
3. 🟠 **チキン南蛮弁当（BATCH-BENTO-001）**：残30個、賞味期限まで残り約18時間

最切迫商品のたまごサンド（BATCH-SAND-001）について、夕方ピーク30%OFFの直前割プロモーションを提案いたします。

```json:proposal
{{
  ""batchCode"": ""BATCH-SAND-001"",
  ""promotionType"": ""DIRECT_DISCOUNT"",
  ""discountPercent"": 30,
  ""name"": ""こだわりたまごサンド 夕方直前割 30% OFF"",
  ""reasoning"": ""賞味期限まで残り6時間の最切迫ロット（12個）を夕方ピークに集中完売させるため、30%OFFを推奨。""
}}
```");
        }

        return Task.FromResult(@"店長、お疲れ様です。RetailRescue AI アシスタントです。
現在庫データと販売予測をもとに、賞味期限切れ間近商品の廃棄防止プロモーションをご提案できます。

例：「たまごサンドを30%引きにしたい」「チキン弁当の割引を相談したい」「本日の危険な商品はどれ？」などとお気軽にお声がけください。");
    }
}

