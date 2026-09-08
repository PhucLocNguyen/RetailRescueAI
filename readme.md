# 🛒 Retail Rescue AI

### AI Agent for Retail Waste Prevention & Smart Promotion

> **Turning Retail Data into Smarter Store Decisions**

Retail Rescue AI is an **AI Agent-powered retail decision support system** designed to help supermarkets identify products at risk of expiry and recommend suitable promotional actions to reduce product waste.

Instead of simply displaying inventory data, the system connects **inventory, expiry, sales, and promotion data** with AI Agents to transform retail data into actionable recommendations for store managers and staff.

---

## 🎯 The Problem

In supermarkets, products such as:

* Bento
* Sandwich
* Bakery
* Salad
* Dairy products
* Ready-to-eat food

often have a short shelf life.

When products are approaching their expiry date, stores need to make quick decisions:

* Should the product be discounted?
* How much should the discount be?
* When should the promotion start?
* How long should it run?
* Can the remaining inventory be sold before expiry?
* Would a promotion still maintain an acceptable profit margin?

Traditionally, these decisions may depend heavily on manual monitoring and staff experience.

The result can be:

```text
Inventory
    ↓
Product approaches expiry
    ↓
No appropriate action
    ↓
Product remains unsold
    ↓
Product is discarded
    ↓
Revenue loss + Food waste
```

---

# 💡 Our Solution

Retail Rescue AI introduces an **AI Agent layer on top of retail data**.

The system continuously analyzes products approaching expiry and combines multiple sources of information:

```text
Inventory Data
      +
Expiry Data
      +
Sales History
      +
Promotion History
      +
Business Rules
      ↓
   AI Agents
      ↓
Promotion Recommendation
      ↓
Manager Decision
      ↓
Staff Action
      ↓
Sales Result
```

The goal is not to let AI make decisions completely autonomously.

Instead:

> **AI analyzes and recommends.
> Humans review and decide.**

This creates a **Human-in-the-loop AI system** for retail operations.

---

# 🤖 What Does the AI Agent Do?

The AI Agent helps answer a practical retail question:

> **"What should the store do with products that are at risk of expiring?"**

For example:

```text
Chicken Bento

Current Stock:       30 units
Normal Price:        ¥500
Expiry Remaining:    18 hours
Average Sales:       12 units/day
```

The system detects that normal sales may not be enough to sell all remaining inventory.

The AI then analyzes:

```text
✓ Current inventory
✓ Expiry time
✓ Sales velocity
✓ Historical sales
✓ Previous promotions
✓ Promotion effectiveness
✓ Product margin
✓ Business rules
```

and may recommend:

```text
Recommended Action:

20% OFF
17:00 - 22:00

Expected Sales:
24 - 28 units

Risk Level:
HIGH
```

The recommendation is accompanied by evidence explaining **why** the promotion was selected.

---

# 🧠 Multi-Agent Architecture

Retail Rescue AI uses specialized Agents instead of relying on a single AI chatbot.

```text
                       Manager / Staff
                              │
                              ▼
                     ┌─────────────────┐
                     │  Orchestrator   │
                     │      Agent      │
                     └────────┬────────┘
                              │
             ┌────────────────┼────────────────┐
             │                │                │
             ▼                ▼                ▼
      ┌────────────┐   ┌────────────┐   ┌─────────────┐
      │   Expiry   │   │   Sales    │   │ Promotion   │
      │   Agent    │   │   Agent    │   │   Agent     │
      └─────┬──────┘   └─────┬──────┘   └──────┬──────┘
            │                │                  │
            └────────────────┼──────────────────┘
                             ▼
                      ┌─────────────┐
                      │   Reviser   │
                      │   Agent     │
                      └──────┬──────┘
                             │
                             ▼
                     AI Recommendation
                             │
                             ▼
                      Manager Approval
```

---

## 🔎 Expiry Agent

Responsible for detecting products with a high probability of becoming waste.

It considers:

* Current stock
* Expiry time
* Product category
* Store inventory

Example:

```text
30 units
18 hours remaining
      ↓
HIGH EXPIRY RISK
```

---

## 📊 Sales Analysis Agent

Analyzes sales behavior to estimate whether the remaining products can be sold normally.

It considers:

* Average daily sales
* Hourly sales velocity
* Historical sales
* Day-of-week patterns
* Current inventory

Example:

```text
Current Stock:
30

Expected Normal Sales:
9

Potential Unsold:
21
```

---

## 🏷️ Promotion Agent

Uses the available retail context to recommend an appropriate action.

Possible actions include:

```text
NO ACTION
DISCOUNT
BUNDLE
STAFF RECOMMENDATION
```

For discounts, the Agent can recommend:

```text
10%
20%
30%
```

depending on the product situation and business constraints.

---

## 🛡️ Reviser Agent

AI recommendations should not directly modify business-critical systems.

The Reviser validates recommendations against deterministic business rules.

For example:

```text
Maximum Discount
Minimum Margin
Promotion Duration
Expiry Time
Product Status
```

Example:

```text
AI Recommendation
       ↓
    Reviser
       ↓
Business Rules
       ↓
Valid / Invalid
```

This creates an additional safety layer between AI and business operations.

---

# 👨‍💼 Human-in-the-Loop

Retail Rescue AI does not automatically activate promotions.

The workflow is:

```text
AI
 ↓
Analyze
 ↓
Recommend
 ↓
Explain
 ↓
Manager Review
 ↓
┌──────────┬──────────┐
│          │          │
Approve   Modify    Reject
│
▼
Promotion
 ↓
Staff
 ↓
Customer
```

This allows managers to remain in control of important business decisions.

---

# 🏪 How It Is Applied in a Supermarket

Retail Rescue AI can sit on top of existing retail systems such as:

```text
             Supermarket
                  │
       ┌──────────┼──────────┐
       │          │          │
      POS      Inventory   ID-POS
       │          │          │
       └──────────┼──────────┘
                  ▼
             Retail Data
                  │
                  ▼
        ┌──────────────────┐
        │  Retail Rescue   │
        │       AI         │
        └────────┬─────────┘
                 │
                 ▼
       Promotion Recommendation
                 │
          ┌──────┴──────┐
          ▼             ▼
       Manager         Staff
          │             │
          └──────┬──────┘
                 ▼
              Customer
                 │
                 ▼
                POS
```

The system therefore acts as an **AI decision-support layer** between retail data and store operations.

---

# 📱 Main User Experience

## Manager Dashboard

The Manager can see:

```text
Today's Overview

At-Risk Products       12
Critical Products       4
Potential Waste       ¥45,000
AI Recommendations      7
Pending Approval        3
```

---

## Expiry Risk List

| Product       | Stock | Expiry | Risk        | AI Recommendation |
| ------------- | ----: | -----: | ----------- | ----------------- |
| Chicken Bento |    30 |    18h | 🔴 High     | 20% OFF           |
| Sandwich      |    12 |     8h | 🔴 Critical | 30% OFF           |
| Salad         |    20 |    32h | 🟡 Medium   | Monitor           |
| Milk          |     8 |    72h | 🟢 Low      | No Action         |

---

# 💬 AI Assistant

Managers can communicate with the AI Agent.

For example:

```text
Manager:
Why do you recommend 20%?

AI:
The product has 30 units remaining and
only 18 hours until expiry.

Normal sales are approximately 12 units/day.

A previous 20% promotion also resulted
in significantly higher sales.

Therefore, 20% is recommended.
```

The Manager can then ask:

```text
Can we use 15% instead?
```

The AI recalculates the expected impact and provides a new recommendation.

---

# 👨‍🏪 Staff Experience

After approval, Staff receives the promotion information.

```text
🔔 Today's Recommended Product

Chicken Bento

20% OFF

17:00 - 22:00

Reason:
High expiry risk

Suggested Action:
Recommend this product to customers
during the promotion period.
```

This connects AI recommendations with actual store operations.

---

# 📈 Measuring Business Impact

The system should not stop at making recommendations.

It also measures the result.

Example:

### Before Promotion

```text
At-Risk Inventory:
30 units

Potential Waste:
21 units
```

### After Promotion

```text
Sold:
26 units

Remaining:
4 units

Waste Avoided:
17 units
```

The system can then calculate:

```text
Waste Reduction
=
Avoided Waste
───────────────
Potential Waste
```

Example:

```text
17 / 21 = 81%
```

This allows the business to evaluate whether the AI recommendation actually created value.

---

# 🔄 End-to-End Flow

The complete business flow is:

```text
1. Retail Data
      ↓
2. Detect Expiry Risk
      ↓
3. Analyze Sales
      ↓
4. Analyze Promotion History
      ↓
5. Generate Promotion Recommendation
      ↓
6. Validate Business Rules
      ↓
7. Manager Review
      ↓
8. Approve / Modify / Reject
      ↓
9. Staff Notification
      ↓
10. Customer Interaction
      ↓
11. Sales
      ↓
12. Measure Waste Reduction
```

---

# 🧩 Data Used by AI

The AI Agent can work with multiple retail data sources.

### Inventory

```text
Product
Quantity
Batch
Expiry
Store
```

### Sales

```text
Product
Quantity
Price
Time
Store
```

### Promotion

```text
Discount
Duration
Sales Uplift
Promotion Result
```

### Business Rules

```text
Maximum Discount
Minimum Margin
Promotion Duration
Product Restrictions
```

The combination of these datasets allows the Agent to make more meaningful recommendations.

---

# 🏗️ System Architecture

```text
┌────────────────────────────────────────────┐
│                 Frontend                   │
│                                            │
│             Next.js / React               │
│                                            │
│  Manager Dashboard     Staff Dashboard    │
│  AI Chat               Promotion View     │
└───────────────────┬────────────────────────┘
                    │
                    ▼
┌────────────────────────────────────────────┐
│                  Backend                   │
│                                            │
│              ASP.NET Core                 │
│                                            │
│ Product API                                │
│ Inventory API                              │
│ Sales API                                  │
│ Promotion API                              │
│ AI Recommendation API                      │
└───────────────────┬────────────────────────┘
                    │
                    ▼
┌────────────────────────────────────────────┐
│               AI Agent Layer               │
│                                            │
│ Orchestrator                               │
│ Expiry Agent                               │
│ Sales Analysis Agent                       │
│ Promotion Agent                            │
│ Reviser                                    │
└───────────────────┬────────────────────────┘
                    │
                    ▼
┌────────────────────────────────────────────┐
│                Retail Data                 │
│                                            │
│ Products                                   │
│ Inventory                                  │
│ Sales                                      │
│ Promotions                                 │
│ Promotion Results                          │
└────────────────────────────────────────────┘
```

---

# 🛠️ Technology Stack

The initial implementation can use:

### Frontend

* Next.js
* React
* TypeScript
* Tailwind CSS
* shadcn/ui

### Backend

* ASP.NET Core Web API
* C#
* Entity Framework Core

### Database

* SQL Server

### AI

The AI layer can be integrated with an LLM provider through an abstraction layer.

Possible providers:

* OpenAI
* Gemini
* Claude

The business logic should remain independent from a specific LLM provider.

---

# 🎬 Demo Scenario

The MVP demonstration focuses on a single real-world scenario.

```text
Chicken Bento
30 units
18 hours until expiry
```

### Step 1

System detects:

```text
⚠️ HIGH EXPIRY RISK
```

### Step 2

AI analyzes:

```text
Inventory
Sales History
Promotion History
Business Rules
```

### Step 3

AI recommends:

```text
20% OFF
17:00 - 22:00
```

### Step 4

Manager asks:

```text
Why 20%?
```

AI explains the recommendation using available data.

### Step 5

Manager approves the promotion.

### Step 6

Staff receives the promotion.

### Step 7

Simulated sales data is generated.

### Step 8

Dashboard shows:

```text
Potential Waste
        ↓
     21 units

Actual Waste
        ↓
      4 units

Waste Reduction
        ↓
       81%
```

---

# 🌟 Why This Project?

The project is built around a simple idea:

> **Retail data becomes valuable when it can support better decisions.**

Traditional analytics can tell a Manager:

```text
"What happened?"
```

AI can help answer:

```text
"Why did it happen?"
```

and more importantly:

```text
"What should we do next?"
```

Retail Rescue AI aims to connect these three stages:

```text
DATA
 ↓
INSIGHT
 ↓
ACTION
```

---

# 🆚 Difference from Traditional Inventory AI

Traditional inventory optimization often focuses on:

> **"What should we order?"**

Retail Rescue AI focuses on:

> **"What should we do with inventory that is already at risk?"**

### Replenishment

```text
Sales
+
Inventory
 ↓
Demand Forecast
 ↓
Order Recommendation
```

### Retail Rescue AI

```text
Inventory
+
Expiry
+
Sales
+
Promotion History
 ↓
AI Analysis
 ↓
Promotion Recommendation
```

The focus is therefore:

**Waste Prevention + Promotion Optimization**

---

# 🚀 Future Vision

Retail Rescue AI can eventually evolve from an expiry-prevention solution into a broader **Retail AI Decision Support Platform**.

Potential future Agents include:

```text
Retail AI Platform
│
├── Expiry Agent
│   └── Reduce waste
│
├── Promotion Agent
│   └── Optimize promotions
│
├── Demand Agent
│   └── Forecast demand
│
├── Inventory Agent
│   └── Optimize stock
│
└── Customer Agent
    └── Personalize recommendations
```

Additional context can also be introduced:

```text
Weather
+
Holidays
+
Local Events
+
Customer Behavior
+
ID-POS Data
```

This could allow the system to make increasingly context-aware retail recommendations.

---

# 🎯 Project Goals

The MVP aims to demonstrate that AI Agents can:

* Detect expiry risks.
* Analyze retail data.
* Recommend appropriate actions.
* Explain recommendations.
* Respect business rules.
* Keep humans in control.
* Connect recommendations to store staff.
* Measure business impact.

The key principle is:

> **AI should not simply generate answers.
> AI should help turn data into business actions.**

---

# 📌 Project Summary

**Retail Rescue AI** is an AI Agent-powered solution for supermarkets that transforms retail data into actionable recommendations for reducing product waste.

```text
             RETAIL DATA
                  │
                  ▼
             AI AGENTS
                  │
                  ▼
        SMART RECOMMENDATION
                  │
                  ▼
          MANAGER DECISION
                  │
                  ▼
            STAFF ACTION
                  │
                  ▼
             CUSTOMER
                  │
                  ▼
           BUSINESS RESULT
```

### Product Statement

> **Retail Rescue AI uses retail data and AI Agents to detect products at risk of expiry and recommend smart promotional actions, helping stores reduce waste while maintaining business constraints.**

---

## 📂 Project Status

**Current Phase:** 🟡 Design

```text
[x] Concept
[x] Problem Definition
[x] Solution Definition
[x] AI Agent Concept

[ ] Business Requirements
[ ] User Stories
[ ] User Flow
[ ] UI/UX Design
[ ] Database Design
[ ] API Design
[ ] Agent Implementation
[ ] Frontend Implementation
[ ] Backend Implementation
[ ] End-to-End Demo
```

---

## 👨‍💻 Development Direction

The project will be developed incrementally:

```text
Phase 1
Product & Business Design
        ↓
Phase 2
System & Database Design
        ↓
Phase 3
Agent & AI Design
        ↓
Phase 4
Backend + Agent Implementation
        ↓
Phase 5
Frontend Implementation
        ↓
Phase 6
Integration
        ↓
Phase 7
Demo & Evaluation
```

---

## 📄 License

This project is currently developed as a prototype / demonstration project.
