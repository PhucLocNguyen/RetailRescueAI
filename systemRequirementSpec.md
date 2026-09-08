# RetailRescue AI

## Business System Requirements

> **Project:** RetailRescue AI
> **Tagline:** AI Agent for Retail Waste Prevention & Smart Promotion
> **Document Type:** Business & System Requirements
> **Status:** Design Phase

---

# 1. Product Overview

## 1.1. Product Vision

RetailRescue AI is an AI-powered retail decision-support system designed for supermarkets and retail stores.

The system combines:

* Inventory data
* Inventory batch and expiry data
* Historical sales data
* Real-time POS transactions
* Promotion history
* Customer purchase history
* Business rules

to identify products at risk of expiry and recommend appropriate promotional actions.

The core philosophy is:

> **AI recommends. Manager decides. Staff executes. Customer purchases. System measures the result.**

The system does not allow AI to directly activate promotions.

---

# 2. Business Problem

Supermarkets frequently face products with limited shelf life.

When products approach their expiry date, store managers need to decide:

* Should the product be discounted?
* How much should the discount be?
* When should the promotion start?
* When should it end?
* Should the product be bundled with another product?
* Should the product be promoted together with products currently being purchased?
* Will the promotion negatively affect the product margin?
* Is the remaining inventory likely to be sold normally without a promotion?

Making these decisions manually can be difficult when the store has a large amount of inventory and sales data.

RetailRescue AI aims to turn this data into actionable recommendations.

---

# 3. Business Objectives

The system should help stores:

1. Reduce expired product waste.
2. Improve sell-through rate for products approaching expiry.
3. Recover revenue from potentially wasted inventory.
4. Recommend appropriate promotions based on data.
5. Help managers make faster promotion decisions.
6. Help staff discover relevant promotions during POS transactions.
7. Use customer purchase history to improve future recommendations.
8. Measure the effectiveness of promotions.
9. Build a foundation for future ID-POS and retail-data analytics.

---

# 4. Core Business Concept

The main business loop is:

```text
Retail Data
    ↓
AI Analysis
    ↓
Expiry Risk Detection
    ↓
Sales Analysis
    ↓
Promotion Recommendation
    ↓
Manager Review
    ↓
Approve / Reject
    ↓
Approved Promotion
    ↓
Staff POS
    ↓
Customer Purchase
    ↓
Sales Data
    ↓
Promotion Result Analysis
    ↓
Future AI Recommendation
```

The system therefore creates a continuous feedback loop between:

> **Inventory → AI → Promotion → POS → Sales → AI**

---

# 5. User Roles

The system has exactly two primary roles:

```text
1. Staff
2. Manager
```

---

# 6. Staff

## 6.1. Staff Responsibilities

Staff members operate the store POS.

Their primary responsibilities are:

* Search products.
* Scan products.
* Add products to cart.
* View applicable promotions.
* Recommend relevant promotions to customers.
* Process payment.
* Complete sales transactions.

Staff does not manage AI Agents.

Staff does not approve AI recommendations.

---

# 7. Staff POS Flow

After login:

```text
Staff Login
    ↓
POS
```

The Staff should be taken directly to the POS screen.

## 7.1. POS Main Functions

The POS should contain:

* Product search
* Product scanning
* Shopping cart
* Customer/member selection
* Promotion recommendation
* Payment
* Transaction completion

Example:

```text
┌───────────────────────────────────────────────┐
│ POS                                           │
├───────────────────────┬───────────────────────┤
│ Product Search        │ Current Cart           │
│                       │                       │
│ [ Scan / Search ]     │ Chicken Bento   ¥500  │
│                       │ Salad          ¥200   │
│                       │                       │
│ Products              │ Total:         ¥700   │
│                       │                       │
│                       │ [ Payment ]           │
├───────────────────────┴───────────────────────┤
│ 💡 Recommended Promotion                       │
│                                               │
│ Salad - 20% OFF                               │
│ Add Salad to receive the promotion.           │
│                                               │
│ [ Recommend to Customer ]                     │
└───────────────────────────────────────────────┘
```

---

# 8. POS Promotion Recommendation

When Staff scans or adds a product to the cart, the system checks whether there are approved promotions relevant to the current transaction.

The POS should not call an LLM for every transaction.

Instead:

```text
POS
 ↓
Promotion API
 ↓
Approved Promotions
 ↓
Business Rules
 ↓
Applicable Promotions
 ↓
POS Recommendation
```

The recommendation should be fast and deterministic.

---

# 9. Promotion Types

RetailRescue AI supports three primary promotion types.

## 9.1. Type A — Direct Discount

A product receives a direct discount.

Example:

```text
Chicken Bento
Normal Price: ¥500
Promotion: 20% OFF
Final Price: ¥400
```

---

## 9.2. Type B — Buy X → Discount Y

A customer purchases one product and receives a discount on another product.

Example:

```text
Buy:
Chicken Bento

Get:
Salad 20% OFF
```

POS:

```text
Customer Cart

✓ Chicken Bento

💡 Recommended Promotion

Add Salad to receive 20% OFF.

[ Recommend to Customer ]
```

---

# 10. Type C — Bundle / Combo

Multiple products are combined into a promotional package.

Example:

```text
Chicken Bento + Salad + Drink

Normal Total: ¥900
Combo Price: ¥750
```

The system should support bundle conditions and promotional pricing.

---

# 11. Promotion Lifecycle

Promotion created by AI or Manager follows:

```text
PENDING
   │
   ├──────────────► APPROVED
   │
   └──────────────► REJECTED
```

There should be no direct:

```text
AI → ACTIVE
```

The system requires Manager approval.

---

# 12. Manager

## 12.1. Manager Responsibilities

Managers operate the Management Portal.

The Manager can:

* View store dashboard.
* Monitor inventory.
* Monitor expiry-risk products.
* View AI recommendations.
* Review AI reasoning/evidence.
* Approve AI recommendations.
* Reject AI recommendations.
* Create promotions through chatbot.
* Review pending promotions.
* Monitor active promotions.
* Monitor promotion results.
* Monitor waste reduction.
* Monitor sales performance.

---

# 13. Manager Login Flow

After login:

```text
Manager Login
    ↓
Management Portal
```

Suggested navigation:

```text
Dashboard
Inventory
Expiry Risk
AI Recommendations
Promotions
Sales
Customers
Results
```

---

# 14. Manager Dashboard

The dashboard should provide an overview of the store.

Suggested KPI cards:

```text
At-Risk Products
Critical Products
Potential Waste
Pending AI Recommendations
Active Promotions
Recovered Revenue
Waste Reduction
Sell-through Rate
```

Example:

```text
┌──────────────┬──────────────┬──────────────┐
│ At-Risk      │ Critical     │ Potential    │
│ Products     │ Products     │ Waste        │
│ 24           │ 8            │ ¥125,000     │
└──────────────┴──────────────┴──────────────┘

┌──────────────┬──────────────┬──────────────┐
│ Pending      │ Active       │ Waste        │
│ AI Actions   │ Promotions   │ Reduction    │
│ 7            │ 12           │ 68%          │
└──────────────┴──────────────┴──────────────┘
```

---

# 15. Inventory Management

Inventory must be managed at **batch level**.

This is important because the same product may have different expiry dates.

Example:

```text
Product: Chicken Bento

Batch A
Stock: 10
Expiry: 2026-09-08 20:00

Batch B
Stock: 30
Expiry: 2026-09-09 20:00

Batch C
Stock: 50
Expiry: 2026-09-11 20:00
```

The AI must analyze batches independently.

---

# 16. Inventory Batch Requirements

Each inventory batch should contain at least:

* Product
* Batch identifier
* Quantity
* Remaining quantity
* Production date
* Expiry date
* Store
* Inventory status

Possible statuses:

```text
AVAILABLE
LOW_STOCK
AT_RISK
EXPIRED
SOLD_OUT
```

---

# 17. Expiry Risk

The system should calculate expiry risk based on factors such as:

* Remaining quantity
* Remaining time until expiry
* Historical sales velocity
* Recent sales velocity
* Product category
* Promotion history
* Store-level demand

Example:

```text
Product:
Chicken Bento

Remaining Stock:
30

Time Until Expiry:
18 hours

Average Sales:
12 units/day

Risk:
CRITICAL
```

The system should prioritize products where the estimated remaining sales are insufficient to sell the remaining inventory before expiry.

---

# 18. AI Agent Architecture

RetailRescue AI uses multiple specialized AI Agents.

```text
                    Orchestrator Agent
                           │
          ┌────────────────┼────────────────┐
          │                │                │
          ▼                ▼                ▼
    Expiry Agent     Sales Agent      Promotion Agent
          │                │                │
          └────────────────┼────────────────┘
                           ▼
                     Reviser Agent
                           │
                           ▼
                  AI Recommendation
                           │
                           ▼
                        Manager
```

---

# 19. Orchestrator Agent

The Orchestrator coordinates the AI analysis process.

Responsibilities:

* Start analysis.
* Determine which agents should run.
* Pass relevant data between agents.
* Collect agent outputs.
* Create a unified recommendation.

The Orchestrator should not directly approve promotions.

---

# 20. Expiry Agent

The Expiry Agent identifies products/batches at risk of expiry.

Input:

* Inventory batches
* Expiry dates
* Current stock
* Product information

Output:

```text
Product
Batch
Risk Level
Remaining Quantity
Hours Until Expiry
```

Example:

```json
{
  "product": "Chicken Bento",
  "batch": "BATCH-001",
  "remainingQuantity": 30,
  "hoursUntilExpiry": 18,
  "riskLevel": "CRITICAL"
}
```

---

# 21. Sales Analysis Agent

The Sales Analysis Agent analyzes:

* Historical sales
* Recent sales
* Sales velocity
* Time-of-day sales
* Day-of-week patterns
* Product relationships
* Promotion history

Example:

```text
Chicken Bento

Average daily sales: 12
Today's sales: 5
Peak sales period: 17:00–20:00
```

---

# 22. Promotion Agent

The Promotion Agent recommends suitable promotional actions.

Possible actions:

```text
NO_ACTION
DIRECT_DISCOUNT
BUY_X_GET_DISCOUNT
BUNDLE
STAFF_RECOMMENDATION
```

The recommendation should consider:

* Expiry risk
* Stock level
* Sales velocity
* Promotion history
* Expected demand
* Margin constraints
* Promotion duration
* Business rules

---

# 23. Reviser Agent

The Reviser Agent validates the recommendation before presenting it to the Manager.

It checks:

* Promotion validity
* Expiry constraints
* Discount limits
* Margin requirements
* Promotion time
* Stock availability
* Business rules

Example:

```text
AI Recommendation:

20% OFF
17:00–22:00

↓

Reviser Agent

✓ Ends before expiry
✓ Discount within limit
✓ Minimum margin maintained
✓ Stock available

↓

PENDING
```

---

# 24. AI Recommendation Lifecycle

The complete flow:

```text
AI Analysis
    ↓
Recommendation Generated
    ↓
PENDING
    │
    ├──── Manager Approve ────► APPROVED
    │
    └──── Manager Reject ─────► REJECTED
```

Only `APPROVED` recommendations can become usable promotions.

---

# 25. AI Schedule

The AI analysis runs automatically every **3 hours**.

Example:

```text
00:00
03:00
06:00
09:00
12:00
15:00
18:00
21:00
```

Each scheduled execution should:

1. Load latest inventory.
2. Load latest batch information.
3. Load historical sales.
4. Load recent POS transactions.
5. Analyze expiry risk.
6. Analyze sales.
7. Generate promotion recommendations.
8. Validate recommendations.
9. Store recommendations as `PENDING`.

---

# 26. Manual AI Execution

The Manager can manually trigger AI analysis.

Example:

```text
Manager Dashboard

[ Run AI Analysis ]
```

Flow:

```text
Manager
   ↓
Run AI Analysis
   ↓
Orchestrator
   ↓
AI Agents
   ↓
Reviser
   ↓
PENDING Recommendations
```

Manual execution should not automatically approve any recommendation.

---

# 27. Manager Chatbot

The Manager can interact with a chatbot to create promotions.

The chatbot acts as a business assistant.

Example:

```text
Manager:

"Chicken Bento còn 30 hộp và sắp hết hạn.
Tôi muốn tạo chương trình giảm giá."
```

The chatbot analyzes the available business data and proposes a promotion.

```text
Chatbot:

"Chicken Bento hiện có 30 sản phẩm có nguy cơ
hết hạn trong 18 giờ.

Tôi đề xuất giảm 20% từ 17:00 đến 22:00.

Bạn có muốn tạo promotion này không?"
```

Manager:

```text
"Đồng ý."
```

The system creates:

```text
Promotion
Status = PENDING
CreatedBy = Manager
CreatedVia = Chatbot
```

The chatbot must not directly activate the promotion.

---

# 28. Chatbot Promotion Creation Flow

```text
Manager
   ↓
Chatbot
   ↓
Understand Request
   ↓
Retrieve Business Data
   ↓
Analyze Promotion Conditions
   ↓
Generate Promotion Proposal
   ↓
Manager Confirmation
   ↓
Create PENDING Promotion
   ↓
Manager Approval
   ↓
APPROVED
```

---

# 29. Promotion Approval

The Manager should be able to review:

* Product
* Batch
* Current stock
* Expiry
* Proposed promotion
* Discount
* Start time
* End time
* Expected sales
* Expected waste reduction
* Expected revenue
* AI explanation

Example:

```text
Chicken Bento

Stock: 30
Expiry: 18 hours

AI Recommendation:
20% OFF

Promotion:
17:00–22:00

Expected Sell-through:
82%

Expected Waste Reduction:
~70%

[ Approve ] [ Reject ]
```

---

# 30. Customer / Member

The system should support Customer/Member information.

Customer identification is **optional during POS transactions**.

Flow:

```text
Staff
 ↓
POS
 ↓
Optional Customer Selection
 ↓
Purchase
 ↓
Transaction
 ↓
Customer Purchase History
```

---

# 31. Customer Purchase History

The system should store relevant purchase history.

Example:

```text
Customer: C000123

Purchase History:

2026-09-01
- Chicken Bento
- Salad

2026-09-03
- Chicken Bento
- Drink

2026-09-05
- Chicken Bento
```

This information can be used for future recommendation scenarios.

---

# 32. Future Customer Recommendation

Customer purchase history can be used to suggest products during future purchases.

Example:

```text
Customer previously purchased:

Chicken Bento
Salad

Current purchase:

Chicken Bento

↓

System may recommend:

Salad
20% OFF
```

The customer recommendation layer should remain separate from the core expiry-risk engine.

The initial MVP may use simple business rules and historical purchase relationships before introducing more advanced recommendation models.

---

# 33. Sales Data

The system uses two types of sales data:

## Historical Sales

Seeded or imported data representing previous sales.

Example:

```text
Last 30 days
Product sales
Daily sales
Hourly sales
Promotion sales
```

## Real POS Sales

Transactions generated by the actual Staff POS.

```text
Staff POS
   ↓
Transaction
   ↓
Sales Database
```

Both datasets are available to the Sales Analysis Agent.

---

# 34. POS Transaction Flow

```text
Staff Login
    ↓
POS
    ↓
Scan/Search Product
    ↓
Add to Cart
    ↓
Check Approved Promotions
    ↓
Recommend Applicable Promotion
    ↓
Customer Confirms Purchase
    ↓
Payment
    ↓
Create Transaction
    ↓
Update Inventory
    ↓
Update Customer Purchase History
    ↓
Store Sales Data
```

---

# 35. Promotion Applicability

Not every promotion should appear to every customer.

The POS should check:

* Promotion status
* Promotion start time
* Promotion end time
* Product availability
* Cart conditions
* Bundle conditions
* Buy-X conditions
* Customer conditions if applicable

Only applicable promotions should be displayed.

---

# 36. Business Rules

## BR-001 — Manager Approval

AI-generated recommendations must be reviewed by a Manager.

```text
AI → PENDING → Manager → APPROVED / REJECTED
```

---

## BR-002 — AI Cannot Directly Activate Promotions

AI must never directly activate a promotion.

---

## BR-003 — Promotion Must Not Extend Beyond Expiry

For expiry-related promotions:

```text
Promotion End Time <= Product/Batch Expiry Time
```

---

## BR-004 — Batch-Level Expiry

Expiry must be evaluated at batch level.

The system must not assume that all inventory of a product has the same expiry date.

---

## BR-005 — Approved Promotions Only

The Staff POS may only display promotions that are approved and currently applicable.

```text
PENDING → Not available at POS

REJECTED → Not available at POS

APPROVED + Valid Time + Valid Conditions
→ Available at POS
```

---

## BR-006 — Inventory Validation

A promotion must not recommend products that have no available stock.

---

## BR-007 — Promotion Conditions

Each promotion must contain explicit conditions.

Examples:

```text
Direct Discount:
Product = Salad
Discount = 20%

Buy X:
Buy Product A >= 1
Discount Product B = 20%

Bundle:
A + B + C
Combo Price = ¥750
```

---

## BR-008 — Customer Is Optional

A customer/member is not required to complete a POS transaction.

---

## BR-009 — POS Recommendation

The POS recommendation should be generated from approved promotion data and deterministic business rules.

The POS does not need to invoke the LLM for each transaction.

---

## BR-010 — AI Schedule

Automatic AI analysis runs every 3 hours.

Manager can also trigger manual analysis.

---

# 37. Core Data Entities

The initial system should contain at least:

```text
User
Role

Store

Product
ProductCategory

Inventory
InventoryBatch

Sale
SaleItem

Customer
CustomerPurchaseHistory

Promotion
PromotionCondition
PromotionProduct

AIRecommendation
AIRecommendationEvidence

PromotionResult
```

---

# 38. Important Entity Relationships

```text
Store
 │
 ├── Inventory
 │      └── InventoryBatch
 │
 ├── Products
 │
 ├── Sales
 │      └── SaleItem
 │
 ├── Promotions
 │
 └── AIRecommendations


Customer
 │
 └── Sales


Product
 │
 ├── InventoryBatch
 ├── SaleItem
 ├── PromotionProduct
 └── AIRecommendation
```

---

# 39. AI Recommendation Data

An AI recommendation should store enough information for auditing and explanation.

Suggested fields:

```text
Recommendation ID
Product ID
Batch ID
Recommendation Type
Risk Level
Recommended Action
Discount
Start Time
End Time
Expected Sales
Expected Waste Reduction
Expected Revenue
Reason
Evidence
Status
Created At
Reviewed At
Reviewed By
```

---

# 40. Promotion Data

A promotion should contain:

```text
Promotion ID
Name
Type
Status

Start Time
End Time

Discount
Bundle Price

Created By
Created Via

Conditions

Approved By
Approved At

Created At
Updated At
```

Possible `CreatedVia` values:

```text
MANAGER
AI_AGENT
CHATBOT
```

---

# 41. Promotion Result

After a promotion ends, the system should compare expected and actual results.

Metrics may include:

```text
Promotion ID

Original Stock
Stock Before Promotion
Units Sold
Units Remaining
Expired Units

Expected Sales
Actual Sales

Expected Waste Reduction
Actual Waste Reduction

Expected Revenue
Actual Revenue
```

This allows the system to measure whether AI recommendations were effective.

---

# 42. Business KPI

The Management Portal should provide metrics such as:

## Waste Reduction

```text
Waste Reduction Rate =
Avoided Waste / Potential Waste
```

## Sell-through Rate

```text
Sell-through Rate =
Units Sold / Units Available
```

## Promotion Effectiveness

```text
Promotion Effectiveness =
Actual Sales During Promotion
/
Expected Sales Without Promotion
```

## Recovered Revenue

Revenue generated from products that were otherwise at high risk of expiry.

---

# 43. End-to-End Business Scenario

## Scenario: Chicken Bento Near Expiry

Initial inventory:

```text
Product:
Chicken Bento

Batch:
BATCH-001

Stock:
30

Expiry:
18 hours from now
```

Historical sales:

```text
Average:
12 units/day

Peak:
17:00–20:00
```

---

## Step 1 — AI Analysis

Scheduled AI execution starts.

```text
Expiry Agent
    ↓
Detects Critical Risk
```

---

## Step 2 — Sales Analysis

```text
Sales Agent

30 units remaining
Expected normal sales before expiry:
approximately 9 units

Risk:
High
```

---

## Step 3 — Promotion Recommendation

Promotion Agent recommends:

```text
20% OFF
17:00–22:00
```

---

## Step 4 — Reviser

The Reviser checks:

```text
✓ Promotion ends before expiry
✓ Discount is within allowed range
✓ Stock is available
✓ Promotion conditions are valid
```

Recommendation becomes:

```text
PENDING
```

---

## Step 5 — Manager Review

Manager opens:

```text
AI Recommendations
```

Reviews the evidence and chooses:

```text
[ Approve ]
```

Status:

```text
APPROVED
```

---

## Step 6 — POS

A customer purchases:

```text
Chicken Bento
```

The POS detects an applicable promotion.

Example:

```text
💡 Recommended Promotion

Salad
20% OFF

Add Salad to receive the promotion.

[ Recommend to Customer ]
```

Staff can verbally recommend the promotion to the customer.

The AI does not communicate directly with the customer.

---

## Step 7 — Purchase

Customer accepts.

```text
Chicken Bento
+
Salad
```

The POS applies the applicable promotion.

---

## Step 8 — Transaction

```text
Payment
 ↓
Sale Created
 ↓
Inventory Updated
 ↓
Customer Purchase History Updated
```

---

## Step 9 — Result Analysis

After the promotion ends:

```text
Initial At-Risk Stock: 30

Sold: 26

Expired: 4

Waste Reduction:
~87%
```

The result is stored for future analysis.

---

# 44. System Architecture

High-level architecture:

```text
                         LOGIN
                           │
               ┌───────────┴───────────┐
               │                       │
             STAFF                  MANAGER
               │                       │
               ▼                       ▼
              POS              MANAGEMENT PORTAL
               │                       │
               │                ┌──────┼──────┐
               │                │      │      │
               │                ▼      ▼      ▼
               │           Inventory Dashboard AI
               │                              │
               │                         AI Agents
               │                              │
               │                ┌─────────────┼─────────────┐
               │                ▼             ▼             ▼
               │             Expiry         Sales       Promotion
               │                │             │             │
               │                └─────────────┼─────────────┘
               │                              ▼
               │                           Reviser
               │                              │
               │                              ▼
               │                       Recommendation
               │                              │
               │                              ▼
               │                           Manager
               │                              │
               │                       Approve / Reject
               │                              │
               └──────────────────────────────┘
                              │
                              ▼
                      Approved Promotion
                              │
                              ▼
                             POS
                              │
                              ▼
                         Customer
                              │
                              ▼
                       Sales Transaction
                              │
                              ▼
                         Sales Data
                              │
                              ▼
                       Future AI Analysis
```

---

# 45. Suggested Application Structure

## Staff Application

```text
/pos
```

Main screen:

```text
Product Search
Cart
Customer
Promotion Recommendation
Payment
```

---

## Manager Application

```text
/management
/management/inventory
/management/expiry-risk
/management/ai-recommendations
/management/promotions
/management/sales
/management/customers
/management/results
```

---

# 46. Backend API Concept

Suggested API groups:

```text
/api/auth

/api/products

/api/inventory
/api/inventory/batches

/api/sales
/api/sales/transactions

/api/customers

/api/promotions
/api/promotions/conditions

/api/ai/recommendations
/api/ai/run

/api/pos/recommendations

/api/results
```

---

# 47. POS Recommendation API

Example:

```http
GET /api/pos/recommendations
```

Possible response:

```json
{
  "recommendations": [
    {
      "promotionId": "PROMO-001",
      "productId": "SALAD-001",
      "type": "DIRECT_DISCOUNT",
      "discountPercent": 20,
      "reason": "Approved promotion",
      "applicable": true
    }
  ]
}
```

The actual API design should be finalized during the technical design phase.

---

# 48. AI Agent Scheduling

The backend should provide a scheduled job mechanism.

Conceptually:

```text
Scheduler
    │
    ├── Every 3 hours
    │
    ▼
AI Analysis Job
    │
    ▼
Orchestrator
    │
    ├── Expiry Agent
    ├── Sales Agent
    ├── Promotion Agent
    └── Reviser Agent
    │
    ▼
AI Recommendations
```

A manual trigger should use the same analysis pipeline.

---

# 49. Human-in-the-Loop Principle

This is one of the most important principles of the system.

```text
                 AI
                  │
            Analyze Data
                  │
          Generate Proposal
                  │
             Explain Why
                  │
                  ▼
              MANAGER
                  │
          ┌───────┴───────┐
          ▼               ▼
       APPROVE           REJECT
          │
          ▼
      Promotion
          │
          ▼
         POS
          │
          ▼
        STAFF
```

AI provides decision support.

Manager retains business authority.

Staff executes the approved business action.

---

# 50. MVP Scope

The MVP should focus on one strong end-to-end scenario rather than attempting to build a complete supermarket system.

## MVP Features

### Staff

* Login
* POS
* Product search/scan
* Cart
* Optional Customer
* Promotion recommendation
* Payment
* Sales transaction

### Manager

* Login
* Dashboard
* Inventory
* Batch/expiry management
* Expiry-risk list
* AI recommendations
* Recommendation details
* Approve
* Reject
* Promotion management
* Chatbot promotion creation
* Promotion result dashboard

### AI

* Scheduled execution every 3 hours
* Manual execution
* Expiry Agent
* Sales Analysis Agent
* Promotion Agent
* Reviser Agent
* Recommendation generation
* Recommendation explanation

### Data

* Product
* Inventory
* Inventory Batch
* Historical Sales
* POS Sales
* Customer
* Promotion
* AI Recommendation
* Promotion Result

---

# 51. MVP Demo Flow

The recommended presentation demo should take approximately 3–5 minutes.

```text
1. Manager Login
       ↓
2. Dashboard shows at-risk products
       ↓
3. Open Chicken Bento
       ↓
4. Show inventory + expiry + sales data
       ↓
5. Run AI Analysis
       ↓
6. AI recommends promotion
       ↓
7. Manager reviews evidence
       ↓
8. Manager approves
       ↓
9. Staff Login
       ↓
10. POS
       ↓
11. Scan product
       ↓
12. POS recommends applicable promotion
       ↓
13. Staff recommends to customer
       ↓
14. Customer purchases
       ↓
15. Sales transaction created
       ↓
16. Inventory updated
       ↓
17. Result dashboard shows waste reduction
```

---

# 52. Differentiation From Traditional POS

Traditional POS:

```text
Scan
 ↓
Calculate Price
 ↓
Payment
```

RetailRescue AI:

```text
Scan
 ↓
Understand Cart
 ↓
Check Approved Promotions
 ↓
Recommend Relevant Promotion
 ↓
Staff Recommendation
 ↓
Purchase
 ↓
Sales Data
```

The POS becomes not only a transaction system but also an execution point for retail intelligence.

---

# 53. Differentiation From StoreMind

StoreMind focuses primarily on:

> **What should the store order tomorrow?**

RetailRescue AI focuses on:

> **What should the store do with inventory that is already at risk of expiry?**

Therefore:

```text
StoreMind
Demand / Inventory
      ↓
Replenishment
      ↓
What should we order?
```

versus:

```text
RetailRescue AI
Inventory + Expiry + Sales
      ↓
Promotion Decision
      ↓
What should we do with it?
```

This provides a different business value proposition.

---

# 54. Long-Term Vision

The MVP can evolve into a broader Retail AI Decision Support Platform.

Future capabilities may include:

```text
Weather Data
     │
Holiday/Event Data
     │
Customer ID-POS
     │
Multi-store Data
     │
Supplier Data
     │
     ▼
Retail AI Platform
     │
     ├── Waste Prevention
     ├── Promotion Optimization
     ├── Demand Forecasting
     ├── Replenishment
     ├── Customer Recommendation
     └── Store Optimization
```

---

# 55. Future AI Capabilities

Potential future extensions:

1. Weather-aware promotions.
2. Holiday/event-aware demand analysis.
3. Cross-store inventory optimization.
4. Customer-specific recommendations.
5. Demand forecasting.
6. Dynamic promotion optimization.
7. Promotion effectiveness prediction.
8. Automatic product relationship discovery.
9. Multi-store AI orchestration.
10. Advanced ID-POS analytics.

---

# 56. Non-Functional Requirements

## Performance

POS promotion recommendations should return quickly enough to avoid interrupting the checkout process.

Target:

```text
POS recommendation API:
< 500 ms under normal conditions
```

---

## Reliability

POS transactions should not fail simply because the AI Agent service is unavailable.

The POS should continue normal checkout operations.

AI functionality should be treated as an enhancement to the core transaction system.

---

## Auditability

The system should record:

* Who created a promotion.
* Whether it was created by AI or Manager.
* When it was created.
* Who approved/rejected it.
* What recommendation/evidence was used.
* When the promotion became available.

---

## Security

Role-based access control must be enforced.

```text
STAFF
 ↓
POS permissions

MANAGER
 ↓
Management permissions
```

Staff must not be able to:

* Approve AI recommendations.
* Reject AI recommendations.
* Modify business-level promotion rules.
* Access manager-only functions.

---

# 57. Core Design Principles

## Principle 1 — AI Is a Decision Support Layer

AI should help managers make decisions.

It should not replace the manager.

---

## Principle 2 — Human Approval

Important business actions require human approval.

---

## Principle 3 — Data Before AI

AI recommendations should be based on actual retail data whenever possible.

```text
Inventory
+
Expiry
+
Sales
+
Promotion
+
Customer
+
Business Rules
```

---

## Principle 4 — Explainable Recommendations

Every AI recommendation should provide a concise explanation and supporting evidence.

Example:

```text
Why?

30 units remain.
Only 18 hours until expiry.
Average sales are 12 units/day.
Expected normal sales are insufficient
to clear the remaining inventory.

Recommendation:
20% OFF from 17:00–22:00.
```

---

## Principle 5 — AI Does Not Directly Talk to Customers

The AI communicates recommendations to Staff.

```text
AI
 ↓
Staff
 ↓
Customer
```

This keeps the customer interaction under human control.

---

# 58. Definition of Done — MVP

The MVP is considered complete when:

* [ ] Staff can log in.
* [ ] Manager can log in.
* [ ] Staff is redirected to POS.
* [ ] Manager is redirected to Management Portal.
* [ ] Staff can scan/search products.
* [ ] Staff can create a cart.
* [ ] Customer can optionally be associated with a transaction.
* [ ] Staff can complete a payment simulation.
* [ ] Sales transactions are stored.
* [ ] Inventory is updated after sales.
* [ ] Inventory is managed by batch.
* [ ] Expiry risk can be detected.
* [ ] Historical sales data is available.
* [ ] AI can analyze expiry risk.
* [ ] AI can analyze sales.
* [ ] AI can recommend promotions.
* [ ] Reviser Agent validates recommendations.
* [ ] AI analysis runs every 3 hours.
* [ ] Manager can manually run AI analysis.
* [ ] Recommendations start as PENDING.
* [ ] Manager can approve recommendations.
* [ ] Manager can reject recommendations.
* [ ] Manager can create promotions through chatbot.
* [ ] Chatbot-created promotions start as PENDING.
* [ ] Only approved promotions are available to POS.
* [ ] POS can recommend applicable promotions.
* [ ] Promotion results are recorded.
* [ ] Dashboard can show business impact.

---

# 59. Core Product Message

The product should be presented as:

> **RetailRescue AI turns retail data into actionable store decisions.**

The complete value chain is:

```text
POS / ID-POS Data
        ↓
Inventory Data
        ↓
Expiry Risk
        ↓
AI Agents
        ↓
Promotion Recommendation
        ↓
Manager Decision
        ↓
Approved Promotion
        ↓
Staff POS
        ↓
Customer Purchase
        ↓
Sales Data
        ↓
Business Impact
```

The ultimate objective is:

> **Prevent waste, improve sell-through, and turn retail data into practical actions at the store level.**
