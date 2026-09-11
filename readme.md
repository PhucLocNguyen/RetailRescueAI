# RetailRescueAI

小売店・スーパーマーケット向けの食品ロス削減＆ダイナミックプライシングAI。
賞味期限間近のロットを検知して割引・コンボ施策を下書きし、店長（マネージャー）が確認・承認するだけ。
承認されたプロモーションはリアルタイムでPOSレジへ同期され、日中はスタッフへの接客アシスト（AIアップセル）を提供します。

> **Note**: This is the Japanese version of the README. For English, please scroll down.
> **[English version below](#retailrescueai-english)**

## 📉 課題

東京・新宿のある食品スーパーでは、毎週 **40,000円〜60,000円** 分の弁当・おにぎり・サンドイッチが廃棄されています。同じSKU（商品）であっても、昨日入荷したロットと今日入荷したロットが混在し、賞味期限の管理は極めて煩雑です。

夕方のピーク前、店長はバックヤードと売場を行き来しながら、どのロットに何%の値引きシールを貼るべきか、あるいはドリンクとセット（コンボ）にして売るべきかを暗算で判断しています。

こうした判断は繰り返しが多く、精神的にも大きな負担です。毎日のように同じ問いに直面します: *「どのロットが何時間後に切れるか？」「定価のままで完売できるか？」「何%引けば粗利（15%以上）を保ちつつ売り切れるか？」* 食品小売の利益率はわずか **2〜5%** 程度のため、判断を誤れば赤字転落か、大量の廃棄ロスの二者択一になってしまいます。

RetailRescueAIは、この「考える」部分を自動化します。ロット別在庫DB・POS販売履歴・利益率制約・商品カタログへアクセスできる **Microsoft Semantic Kernel** 搭載のエージェントチームが、最適な値引き・コンボ施策を下書きします。店長は確認してワンタップで承認するだけです。

## 仕組み

### 🌑 定期・自動バックグラウンド

店舗運営中および夜間、バックグラウンドスケジューラー（`ExpiryAiScheduledWorker`）または店長のワンクリック実行により、`Code → LLM → Code` のパイプラインが自律動作します:

まず、**ExpiryAgent** がロット在庫DBを走査し、賞味期限までの残時間を計算して危険度（CRITICAL / AT_RISK / EXPIRED）を判定します。賞味期限切れの商品は販売禁止（BR-007）として即時除外されます。
次に、**SalesAgent** が過去7日間のPOS実績から日販ペースを算出し、期限までの自然消化予測と潜在的廃棄損失（¥）を試算します。
続いて、**PromotionAgent**（Google Gemini 2.5 Flash）が直前値引き（20%〜30%）やドリンクとのランチコンボを立案し、店長が納得できる日本語の推奨理由を自動生成します。
最後に、**ReviserAgent** がリテール安全制約（BR-003: 期限30分前終了、BR-006: 実在庫ロット限定、最低粗利率15%維持）を自動検証します。赤字や規約違反となる提案はここで自動修正またはブロックされます。

> 🤖 **ExpiryAgent:** "ロット `BATCH-SAND-001`（たまごサンド 11個）は賞味期限まで残り3.9時間。危険度: CRITICAL" <br>
> 🤖 **SalesAgent:** "日販平均 0.1個/日。自然消化予測は0個、潜在廃棄損失は ¥3,080 と試算" <br>
> 🤖 **PromotionAgent:** "夕方ピーク（17:00〜22:00）に30% OFF（¥196）を提案。または宇治緑茶とのコンボ（¥350、¥70お得）を推奨" <br>
> 🤖 **ReviserAgent:** "✅ 全ガードレール合格: 粗利率35.7%（基準15%以上）、BR-003/BR-006制約をクリア。承認待ち（PENDING）として登録"

### 🌅 朝・マネージャー確認

店長はマネージャーポータル（`/management/ai-recommendations` または `/management/chatbot`）を開き、AIが下書きした提案を確認します。

裏側では5つの専門エージェント（Microsoft Semantic Kernel）が連携しています:

1. **OrchestratorAgent** はパイプライン全体を統括し、実行トレースを可視化。人間の意思決定（Human-in-the-Loop）を尊重し、勝手な自動値引きを防いで「承認待ち（PENDING）」として管理します。

2. **ExpiryAgent**（`InventoryDataPlugin` 経由）はロット単位の賞味期限残時間をリアルタイムに評価します。

3. **SalesAgent**（`SalesVelocityPlugin` 経由）はPOS販売データを解析し、消化ペースと損失額を弾き出します。

4. **PromotionAgent**（Gemini 2.5 Flash + `ComboStrategyPlugin`）は割引率の最適化とコンボ施策を立案し、スタッフ向けの接客スクリプトを作成します。

5. **ReviserAgent**（`SafetyGuardrailPlugin` 経由）はリテール規約と最低粗利15%を死守する防波堤として機能します。

店長は提案カード上で割引率やコンボ価格を自由に微調整し、「承認（承認）」ボタンを押すだけです。また、チャットボットで自然言語による相談も可能です:

> 🗣️ **Manager:** "たまごサンドの在庫状況と、おすすめの割引プロモーションを教えてください。"
>
> 🤖 **Chatbot:** "店長、お疲れ様です。現在、ロット `BATCH-SAND-001` が残り11個、賞味期限まで残り約3.9時間（CRITICAL）です。夕方ピークに向けて30%OFF（¥196）でのプロモーションをご提案します。[承認待ちとして登録]"

### 🕒 日中・POS接客アシスト & 実店舗連動

店長がプロモーションを承認すると、**SignalR** 経由で店頭のPOSレジ（`/pos`）へ即座にリアルタイム通知されます。

スタッフは作業中にPOS画面で常に最新の割引情報を把握でき、さらにAIによる接客支援を受けられます:

1. **AI接客アシスト（AI Upsell Assistant Box）**:
   スタッフが賞味期限間近のたまごサンド（`BATCH-SAND-001`）をレジに通すと、画面上にAI提案枠がポップアップし、お客様へのお声がけセリフが表示されます。

> 🗣️ **Staff (接客トーク):** "「お客様、ご一緒に『宇治緑茶 500ml』はいかがでしょうか？ただいまセットで通常¥420のところ、¥350（¥70お得）でお買い求めいただけます！」"
>
> 👤 **Customer:** "じゃあ、それも一緒にお願いします。"

2. **1-タップ コンボ追加 & ロット厳格減算**:
   スタッフが `[＋ 宇治緑茶を追加]` ボタンを押すだけで、対象ロットの緑茶がカートに追加され、コンボ割引（-¥70）が瞬時に適用されます。
   会計完了時は、SKU単位のざっくり減算ではなく、**対象ロット番号（`BATCH-SAND-001` および `BATCH-DRINK-001`）の在庫数が正確にマイナス**されます。

## エージェント構成

| エージェント | モデル / プラグイン | 何をするか |
|---|---|---|
| **OrchestratorAgent** | C# Orchestrator / SignalR | パイプライン全体の実行管理。各エージェントのログ・所要時間を集約し、Human-in-the-Loopの承認待ちキューを管理します。 |
| **ExpiryAgent** | `InventoryDataPlugin` (SK) | ロット単位で賞味期限をチェックし、緊急度（CRITICAL, AT_RISK, EXPIRED）を判定します。 |
| **SalesAgent** | `SalesVelocityPlugin` (SK) | 直近7日間のPOS売上から日販速度を割り出し、期限までの消化予測と廃棄損失見込額を計算します。 |
| **PromotionAgent** | Google Gemini 2.5 Flash + `ComboStrategyPlugin` (SK) | 直前割（20%〜30%）や食事コンボを立案。日本語の提案理由とPOSスタッフ用接客トークを生成します。 |
| **ReviserAgent** | `SafetyGuardrailPlugin` (SK) | BR-003（期限30分前終了）、BR-006（実ロット在庫）、最低粗利15%ルールを検証し、違反提案をガードします。 |

## 技術スタック

| コンポーネント | バージョン | 説明 |
|-----------|---------|---|
| **.NET SDK** | `9.0.200` | C# 13 / ASP.NET Core Web API |
| **Microsoft.SemanticKernel** | `1.80.1` | オーケストレーション・ネイティブプラグイン基盤 |
| **Microsoft.SemanticKernel.Agents.Core** | `1.80.1` | マルチエージェント抽象化レイヤー |
| **Google Gemini API** | `gemini-2.5-flash` | 日本語推論・コンボトーク生成（MockLLM自動フォールバック付き） |
| **Microsoft.AspNetCore.SignalR** | Native .NET 9 | 店長承認イベントのリアルタイムPOSブロードキャスト |
| **Entity Framework Core** | `9.0.2` | SQLite / SQL Server 対応のロット追跡データアクセス |
| **Next.js** | `15.5.25` | React 19, TypeScript, Tailwind CSS, Lucide Icons |

## クイックスタート

### 1. バックエンドの起動 (.NET 9 Web API)

```bash
cd RetailRescueAI.Backend
dotnet restore
dotnet build
dotnet run --launch-profile http
```

* API サーバー: `http://localhost:5205`
* Swagger UI (OpenAPI v1): `http://localhost:5205/swagger`
* SignalR ハブ: `http://localhost:5205/hubs/promotions`

### 2. フロントエンドの起動 (Next.js 15)

別ターミナルを開き:

```bash
cd RetailRescueAI.Frontend
npm install
npm run dev
```

* 店長ポータル (ダッシュボード): `http://localhost:3000/management`
* AI パイプライン実演 & 承認: `http://localhost:3000/management/ai-recommendations`
* AI 店長チャットボット: `http://localhost:3000/management/chatbot`
* ロット管理・賞味期限モニター: `http://localhost:3000/management/inventory`
* POS レジ端末 (実演画面): `http://localhost:3000/pos`

## 📂 プロジェクト構成

```
RetailRescueAI/
├── RetailRescueAI.Backend/
│   ├── Controllers/          # REST API (AiRecommendations, Chatbot, Pos, Inventory, Promotions)
│   ├── Data/                 # AppDbContext, DbInitializer (日本のコンビニ・スーパー実データ初期投入)
│   ├── DTOs/                 # TraceStepDto, ChatRequest/Response, PosCheckoutDto
│   ├── Hubs/                 # PromotionHub (SignalR によるリアルタイム POS 連動)
│   ├── Models/               # Product, InventoryBatch (ロット単位管理), Promotion, Sale
│   ├── Repositories/         # Repository パターンによるデータアクセス層
│   └── Services/
│       ├── AI/
│       │   ├── Agents/       # ExpiryAgent, SalesAgent, PromotionAgent, ReviserAgent, OrchestratorAgent
│       │   ├── Plugins/      # InventoryDataPlugin, SalesVelocityPlugin, SafetyGuardrailPlugin, ComboStrategyPlugin
│       │   └── SemanticKernel/ # IChatCompletionService 実装 (Gemini 2.5 Flash 連携)
│       ├── BackgroundJobs/   # ExpiryAiScheduledWorker (3時間ごとの定期自律スキャン)
│       └── Implementations/  # PosService, InventoryService, ChatbotService, ResultService
└── RetailRescueAI.Frontend/
    ├── app/
    │   ├── management/       # ai-recommendations, chatbot, inventory, promotions, results
    │   └── pos/              # タッチ式POSレジ、バーコードスキャン、AI接客アシスト枠
    ├── components/           # Navbar, AgentTraceModal, PromotionCard
    └── lib/                  # api.ts (Axios/Fetch クライアント), signalr.ts (SignalR 接続管理)
```

## 設定

`RetailRescueAI.Backend/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=retailrescue.db"
  },
  "Gemini": {
    "ApiKey": "YOUR_GEMINI_API_KEY",
    "Model": "gemini-2.5-flash"
  },
  "Jwt": {
    "Key": "RetailRescueAI_SuperSecretKey_2026_Enterprise_Security_JwtToken!"
  },
  "Scheduler": {
    "IntervalMinutes": 180,
    "RunOnStartup": false
  }
}
```

> **Note**: 有効な Gemini API キーが設定されていない場合でも、組み込みの日本語エンタープライズ対応 `MockLLMService` が自動起動し、完全なデモ動作を保証します。

---

# RetailRescueAI (English)

Multi-Agent AI system for retail food waste prevention and dynamic pricing.
Expiring inventory batches are detected automatically, markdown and meal combo recommendations are drafted, and store managers review and approve with one click.
Approved promotions synchronize in real time to POS cashiers via SignalR, providing store staff with live upsell prompts and strict lot-level inventory tracking.

## 📉 The Problem

In a grocery supermarket in Shinjuku, Tokyo, over **¥40,000–¥60,000** worth of fresh bento boxes, salads, and sandwiches are discarded every week. Products share the same SKU, but exist in multiple inventory batches with completely different manufacturing and expiry timestamps. Managing inventory and markdowns at the lot level is an operational nightmare.

Every evening, store managers pace between the backroom and shelves, doing mental arithmetic: *Which specific batch is expiring? What discount percentage clears inventory before closing? Will a 30% markdown violate the minimum 15% profit margin? Can we bundle it with a beverage as a meal combo?*

With supermarket grocery margins sitting at just **2–5%**, discounting too aggressively causes severe margin bleed, while discounting too late results in 100% waste loss.

RetailRescueAI automates the cognitive burden. Powered by **Microsoft Semantic Kernel**, five specialized AI agents access live batch inventory, POS sales velocity, retail business rules, and product catalogs to draft optimal promotional strategies. The manager simply reviews and approves.

## How It Works

### 🌑 Periodic / Background Autonomous Scan

While the store is operating or overnight, the background scheduler (`ExpiryAiScheduledWorker`) or a manual trigger by the manager runs a `Code → LLM → Code` pipeline:

First, **ExpiryAgent** queries the batch inventory database via Semantic Kernel plugins to evaluate hours remaining until expiration and assign urgency levels (`CRITICAL`, `AT_RISK`, `EXPIRED`). Expired batches are strictly flagged and blocked from sale (BR-007).
Next, **SalesAgent** analyzes 7-day POS transaction velocity to forecast natural clearance before expiry and project potential waste losses in Yen (¥).
Then, **PromotionAgent** (Google Gemini 2.5 Flash) drafts direct percentage markdowns (20%–30%) or pairing bundles (e.g. Tamago Sandwich + Uji Green Tea = ¥350 instead of ¥420) accompanied by compelling business rationale.
Finally, **ReviserAgent** rigorously tests retail safety guardrails:
- **BR-003**: Promotion end time must conclude at least 30 minutes before batch expiration.
- **BR-006**: Discounts only apply to specific inventory batches, never SKU-wide.
- **Margin Protection**: Combined profit margin after discount must strictly remain $\ge 15\%$.

> 🤖 **ExpiryAgent:** "Batch `BATCH-SAND-001` (Egg Sandwich, 11 units) expires in 3.9 hours. Risk: CRITICAL" <br>
> 🤖 **SalesAgent:** "Sales velocity: 0.1 units/day. Normal clearance: 0 units. Projected waste loss: ¥3,080" <br>
> 🤖 **PromotionAgent:** "Propose 30% OFF (¥196) for evening peak (17:00–22:00), or Lunch Combo with Green Tea at ¥350 (¥70 savings)" <br>
> 🤖 **ReviserAgent:** "✅ All guardrails cleared: Gross margin 35.7% ($\ge 15\%$), BR-003/BR-006 verified. Registered as PENDING"

### 🌅 Morning / Manager Review & Chatbot

The store manager opens the portal (`/management/ai-recommendations` or `/management/chatbot`) and reviews the drafted proposals.

Behind the scenes, five specialized Semantic Kernel agents coordinate:

1. **OrchestratorAgent** coordinates the pipeline, logs timing benchmarks, and preserves **Human-in-the-Loop** control by saving proposals as `PENDING (Awaiting Manager Approval)`.

2. **ExpiryAgent** (via `InventoryDataPlugin`) monitors batch expiration hours in real time.

3. **SalesAgent** (via `SalesVelocityPlugin`) inspects POS sales velocity and waste estimates.

4. **PromotionAgent** (Gemini 2.5 Flash + `ComboStrategyPlugin`) generates dynamic pricing, bundle evaluations, and cashier scripts.

5. **ReviserAgent** (via `SafetyGuardrailPlugin`) enforces retail guardrails and prevents selling below acceptable margins.

Managers can tweak discount percentages or combo prices directly on the UI and tap **Approve (承認)**. They can also consult the conversational AI assistant:

> 🗣️ **Manager:** "Show me the status of egg sandwiches and recommend an optimal discount promotion."
>
> 🤖 **Chatbot:** "Good day, Manager! Batch `BATCH-SAND-001` has 11 units remaining, expiring in 3.9 hours (CRITICAL). I recommend a 30% OFF promotion (¥196) during the evening peak to clear inventory safely. [Register as Pending Approval]"

### 🕒 Daytime / POS Upsell Assistant & Real-Time Sync

Once the manager approves a promotion, **SignalR** instantly broadcasts an event to POS terminals (`/pos`).

Cashiers receive real-time notifications and live AI upsell guidance:

1. **AI Upsell Assistant Box (AI 接客アシスト)**:
   When a cashier scans or selects an expiring item (such as `BATCH-SAND-001` Egg Sandwich), an AI upsell box dynamically appears above the cart with a ready-to-read customer script:

> 🗣️ **Staff (Upsell Script):** *"Would you like to pair this with our Uji Green Tea 500ml? You can enjoy the combo set today for just ¥350 instead of the usual ¥420 (save ¥70)!"*
>
> 👤 **Customer:** *"Sure, let's do that!"*

2. **1-Click Combo Addition & Lot-Strict Decrement**:
   The cashier taps `[＋ Add Uji Green Tea]`. The green tea is added immediately, and the -¥70 combo discount is applied.
   Upon checkout, inventory is deducted **strictly from the scanned batch codes (`BATCH-SAND-001` and `BATCH-DRINK-001`)**, guaranteeing zero stock discrepancies between physical lots and digital inventory.

## Agent Configuration

| Agent | Model / Plugin | What it does |
|---|---|---|
| **OrchestratorAgent** | C# Orchestrator / SignalR | Orchestrates the multi-agent pipeline, aggregates trace steps, and manages the Human-in-the-Loop pending queue. |
| **ExpiryAgent** | `InventoryDataPlugin` (SK) | Scans batch-level expiry timestamps and categorizes urgency (CRITICAL, AT_RISK, EXPIRED). |
| **SalesAgent** | `SalesVelocityPlugin` (SK) | Calculates daily sales velocity from 7-day POS logs and projects potential waste losses (¥). |
| **PromotionAgent** | Google Gemini 2.5 Flash + `ComboStrategyPlugin` (SK) | Generates direct discounts (20%–30%) and meal bundles with logical Japanese reasoning and staff scripts. |
| **ReviserAgent** | `SafetyGuardrailPlugin` (SK) | Validates BR-003, BR-006, and ensures gross profit margin remains $\ge 15\%$ before queuing. |

## Tech Stack

| Component | Version | Description |
|---|---|---|
| **.NET SDK** | `9.0.200` | C# 13 / ASP.NET Core Web API |
| **Microsoft.SemanticKernel** | `1.80.1` | Native plugins and AI orchestration kernel |
| **Microsoft.SemanticKernel.Agents.Core** | `1.80.1` | Semantic multi-agent abstractions |
| **Google Gemini API** | `gemini-2.5-flash` | Japanese reasoning & script generation (with automatic MockLLM fallback) |
| **Microsoft.AspNetCore.SignalR** | Native .NET 9 | Real-time POS promotion broadcast |
| **Entity Framework Core** | `9.0.2` | SQLite / SQL Server ORM with batch tracking |
| **Next.js** | `15.5.25` | React 19, TypeScript, Tailwind CSS, Lucide Icons |

## Quick Start

### 1. Launch Backend (.NET 9 Web API)

```bash
cd RetailRescueAI.Backend
dotnet restore
dotnet build
dotnet run --launch-profile http
```

* API Endpoint: `http://localhost:5205`
* Swagger UI (OpenAPI v1): `http://localhost:5205/swagger`
* SignalR Hub: `http://localhost:5205/hubs/promotions`

### 2. Launch Frontend (Next.js 15)

Open a second terminal:

```bash
cd RetailRescueAI.Frontend
npm install
npm run dev
```

* Manager Portal Dashboard: `http://localhost:3000/management`
* AI Multi-Agent Pipeline & Approval: `http://localhost:3000/management/ai-recommendations`
* AI Manager Chatbot: `http://localhost:3000/management/chatbot`
* Batch Inventory & Shelf-Life Monitor: `http://localhost:3000/management/inventory`
* POS Cashier Terminal: `http://localhost:3000/pos`

## 📂 Project Layout

```
RetailRescueAI/
├── RetailRescueAI.Backend/
│   ├── Controllers/          # REST APIs (AiRecommendations, Chatbot, Pos, Inventory, Promotions)
│   ├── Data/                 # AppDbContext, DbInitializer (Japanese supermarket seed data)
│   ├── DTOs/                 # TraceStepDto, ChatRequest/Response, PosCheckoutDto
│   ├── Hubs/                 # PromotionHub (SignalR Real-Time POS Sync)
│   ├── Models/               # Product, InventoryBatch (Lot-level tracking), Promotion, Sale
│   ├── Repositories/         # Repository pattern data access layer
│   └── Services/
│       ├── AI/
│       │   ├── Agents/       # ExpiryAgent, SalesAgent, PromotionAgent, ReviserAgent, OrchestratorAgent
│       │   ├── Plugins/      # InventoryDataPlugin, SalesVelocityPlugin, SafetyGuardrailPlugin, ComboStrategyPlugin
│       │   └── SemanticKernel/ # IChatCompletionService implementation bridging Gemini 2.5 Flash
│       ├── BackgroundJobs/   # ExpiryAiScheduledWorker (3-hour periodic autonomous scheduler)
│       └── Implementations/  # PosService, InventoryService, ChatbotService, ResultService
└── RetailRescueAI.Frontend/
    ├── app/
    │   ├── management/       # ai-recommendations, chatbot, inventory, promotions, results
    │   └── pos/              # Touch POS terminal, barcode scanning, AI upsell assistant box
    ├── components/           # Navbar, AgentTraceModal, PromotionCard
    └── lib/                  # api.ts (REST clients), signalr.ts (SignalR real-time client)
```

## Configuration

`RetailRescueAI.Backend/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=retailrescue.db"
  },
  "Gemini": {
    "ApiKey": "YOUR_GEMINI_API_KEY",
    "Model": "gemini-2.5-flash"
  },
  "Jwt": {
    "Key": "RetailRescueAI_SuperSecretKey_2026_Enterprise_Security_JwtToken!"
  },
  "Scheduler": {
    "IntervalMinutes": 180,
    "RunOnStartup": false
  }
}
```

> **Note**: If a live Gemini API key is not supplied, the built-in Japanese enterprise `MockLLMService` automatically activates to ensure 100% functional demonstrations out-of-the-box.

## Author
PhucLocNguyen
Contact via email: nguyenphucloc.dev@gmail.com