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
        _logger.LogInformation("[MockLLMService] Answering manager chat in Japanese...");

        var msg = userMessage.ToLower();

        if (msg.Contains("チキン") || msg.Contains("弁当") || msg.Contains("bento"))
        {
            return Task.FromResult(@"チキン南蛮弁当（BATCH-BENTO-001）の在庫状況を確認しました。

現在庫：**30個**
賞味期限まで：**残り約18時間**
定価：¥550（原価：¥320）

通常の販売ペース（平均12個/日）のままだと、約21個が売れ残り廃棄になるリスク（CRITICAL）があります。

**【AIの提案】**
本日 **17:00 〜 22:00** の夕方ピーク帯に **20% OFF（割引後 ¥440）** のプロモーションを実施することを推奨します。
20%引き後でも粗利益¥120（粗利率27%）が確保され、最低利益率基準（15%）をクリアしています。

このプロモーションを「承認待ち（PENDING）」として登録しますか？");
        }

        if (msg.Contains("サラダ") || msg.Contains("salad"))
        {
            return Task.FromResult(@"彩りサーモンサラダ（BATCH-SALAD-001）は現在庫25個、賞味期限まで残り26時間です。
チキン南蛮弁当との併売（Buy Bento, Get Salad 20% OFF）を実施することで、客単価アップとサラダの早期完売が期待できます。");
        }

        if (msg.Contains("承認") || msg.Contains("作成") || msg.Contains("お願い") || msg.Contains("はい") || msg.Contains("登録"))
        {
            return Task.FromResult(@"承知いたしました。対象のプロモーション提案を「承認待ち（PENDING）」として作成しました。
マネージャーポータルの「AI Recommendations」または「Promotions」画面にて最終確認・承認を行ってください。");
        }

        return Task.FromResult(@"店長、お疲れ様です。RetailRescue AI アシスタントです。
現在庫データと販売予測をもとに、賞味期限切れ間近商品の廃棄防止プロモーションをご提案できます。
例：「チキン弁当の在庫と割引を相談したい」「現在の危険な商品は？」などとお気軽にお声がけください。");
    }
}

