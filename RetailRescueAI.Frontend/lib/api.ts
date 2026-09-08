const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5000/api';

export interface PosProduct {
  id: number;
  productCode: string;
  name: string;
  description: string;
  categoryName: string;
  price: number;
  barcode: string;
  imageUrl: string;
  totalAvailableStock: number;
  earliestExpiryFormatted: string;
}

export interface PosRecommendationItem {
  promotionId: number;
  promotionCode: string;
  promotionName: string;
  promotionType: string;
  targetProductId: number;
  targetProductName: string;
  originalPrice: number;
  discountPercent: number;
  finalPrice: number;
  message: string;
  actionPrompt: string;
}

export interface InventoryBatch {
  id: number;
  batchCode: string;
  productId: number;
  productName: string;
  productCode: string;
  categoryName: string;
  price: number;
  initialQuantity: number;
  remainingQuantity: number;
  productionDate: string;
  expiryDate: string;
  hoursUntilExpiry: number;
  status: string;
  statusJapanese: string;
}

export interface ExpiryRiskItem {
  batchId: number;
  batchCode: string;
  productId: number;
  productName: string;
  barcode: string;
  remainingQuantity: number;
  hoursUntilExpiry: number;
  averageDailySales: number;
  estimatedNormalSalesUntilExpiry: number;
  potentialWasteUnits: number;
  potentialWasteCost: number;
  riskLevel: string;
  riskJapanese: string;
}

export interface AIRecommendation {
  id: number;
  recommendationCode: string;
  targetProductId: number;
  targetProductName: string;
  targetBatchId: number;
  targetBatchCode: string;
  recommendationType: string;
  riskLevel: string;
  recommendedAction: string;
  recommendedDiscountPercent?: number;
  recommendedComboPrice?: number;
  startTime: string;
  endTime: string;
  expectedSales: number;
  expectedWasteReduction: number;
  expectedRevenue: number;
  reason: string;
  status: string;
  createdAt: string;
  evidences: { key: string; value: string; description: string }[];
}

export interface Promotion {
  id: number;
  promotionCode: string;
  name: string;
  promotionType: string;
  status: string;
  targetProductId?: number;
  targetProductName?: string;
  targetBatchCode?: string;
  discountPercent?: number;
  comboPrice?: number;
  startTime: string;
  endTime: string;
  createdVia: string;
  createdBy: string;
  approvedBy?: string;
  approvedAt?: string;
  aiReasoning?: string;
  createdAt: string;
}

export interface DashboardStats {
  atRiskProductsCount: number;
  criticalProductsCount: number;
  potentialWasteCost: number;
  pendingAiRecommendationsCount: number;
  activePromotionsCount: number;
  wasteReductionRate: number;
  recoveredRevenueTotal: number;
  totalItemsSaved: number;
}

export interface PromotionResult {
  id: number;
  promotionId: number;
  promotionCode: string;
  promotionName: string;
  productName: string;
  batchCode: string;
  initialStock: number;
  stockBeforePromotion: number;
  unitsSold: number;
  unitsRemaining: number;
  expiredUnits: number;
  expectedSales: number;
  actualSales: number;
  actualWasteAvoided: number;
  wasteReductionRate: number;
  actualRevenue: number;
  evaluatedAt: string;
}

export interface Customer {
  id: number;
  customerCode: string;
  name: string;
  phone: string;
  email: string;
  points: number;
}

export async function fetchProducts(search?: string): Promise<PosProduct[]> {
  const url = search ? `${API_BASE_URL}/products?q=${encodeURIComponent(search)}` : `${API_BASE_URL}/products`;
  const res = await fetch(url);
  if (!res.ok) throw new Error('商品一覧の取得に失敗しました。');
  return res.json();
}

export async function fetchBatches(): Promise<InventoryBatch[]> {
  const res = await fetch(`${API_BASE_URL}/inventory/batches`);
  if (!res.ok) throw new Error('ロット一覧の取得に失敗しました。');
  return res.json();
}

export async function fetchExpiryRisks(): Promise<ExpiryRiskItem[]> {
  const res = await fetch(`${API_BASE_URL}/inventory/expiry-risk`);
  if (!res.ok) throw new Error('リスク分析の取得に失敗しました。');
  return res.json();
}

export async function fetchAIRecommendations(): Promise<AIRecommendation[]> {
  const res = await fetch(`${API_BASE_URL}/ai/recommendations`);
  if (!res.ok) throw new Error('AI提案の取得に失敗しました。');
  return res.json();
}

export async function approveAIRecommendation(id: number): Promise<any> {
  const res = await fetch(`${API_BASE_URL}/ai/recommendations/${id}/approve`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({})
  });
  if (!res.ok) throw new Error('AI提案の承認に失敗しました。');
  return res.json();
}

export async function rejectAIRecommendation(id: number, reason: string): Promise<any> {
  const res = await fetch(`${API_BASE_URL}/ai/recommendations/${id}/reject`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ reason })
  });
  if (!res.ok) throw new Error('AI提案の却下に失敗しました。');
  return res.json();
}

export async function triggerManualAiRun(): Promise<any> {
  const res = await fetch(`${API_BASE_URL}/ai/run`, {
    method: 'POST',
  });
  if (!res.ok) throw new Error('AI分析の実行に失敗しました。');
  return res.json();
}

export async function fetchPromotions(status?: string): Promise<Promotion[]> {
  const url = status ? `${API_BASE_URL}/promotions?status=${status}` : `${API_BASE_URL}/promotions`;
  const res = await fetch(url);
  if (!res.ok) throw new Error('プロモーション一覧の取得に失敗しました。');
  return res.json();
}

export async function approvePromotion(id: number): Promise<any> {
  const res = await fetch(`${API_BASE_URL}/promotions/${id}/approve`, {
    method: 'PUT'
  });
  if (!res.ok) throw new Error('プロモーションの承認に失敗しました。');
  return res.json();
}

export async function rejectPromotion(id: number, reason: string): Promise<any> {
  const res = await fetch(`${API_BASE_URL}/promotions/${id}/reject`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ reason })
  });
  if (!res.ok) throw new Error('プロモーションの却下に失敗しました。');
  return res.json();
}

export async function fetchDashboardStats(): Promise<DashboardStats> {
  const res = await fetch(`${API_BASE_URL}/results/dashboard`);
  if (!res.ok) throw new Error('ダッシュボード情報の取得に失敗しました。');
  return res.json();
}

export async function fetchPromotionResults(): Promise<PromotionResult[]> {
  const res = await fetch(`${API_BASE_URL}/results/promotions`);
  if (!res.ok) throw new Error('実績データの取得に失敗しました。');
  return res.json();
}

export async function fetchCustomers(): Promise<Customer[]> {
  const res = await fetch(`${API_BASE_URL}/pos/customers`);
  if (!res.ok) throw new Error('顧客情報の取得に失敗しました。');
  return res.json();
}

export async function fetchPosRecommendations(productIdsInCart: number[], currentSubtotal: number, customerId?: number): Promise<PosRecommendationItem[]> {
  const res = await fetch(`${API_BASE_URL}/pos/recommendations`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ productIdsInCart, currentSubtotal, customerId })
  });
  if (!res.ok) throw new Error('レジ推奨プロモーションの取得に失敗しました。');
  const data = await res.json();
  return data.recommendations || [];
}

export async function processPosCheckout(payload: {
  customerId?: number;
  paymentMethod: string;
  items: { productId: number; quantity: number; appliedPromotionId?: number }[];
  receivedAmount: number;
}): Promise<any> {
  const res = await fetch(`${API_BASE_URL}/pos/checkout`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(payload)
  });
  if (!res.ok) throw new Error('チェックアウト処理に失敗しました。');
  return res.json();
}

export async function sendChatMessage(message: string, history: any[]): Promise<any> {
  const res = await fetch(`${API_BASE_URL}/chatbot/message`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ message, history })
  });
  if (!res.ok) throw new Error('チャット送信に失敗しました。');
  return res.json();
}

