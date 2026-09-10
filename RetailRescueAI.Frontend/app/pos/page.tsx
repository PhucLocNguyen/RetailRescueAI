'use client';

import { useState, useEffect } from 'react';
import Navbar from '@/components/Navbar';
import * as signalR from '@microsoft/signalr';
import {
  PosProduct,
  PosBatchSummary,
  PosRecommendationItem,
  Customer,
  SIGNALR_HUB_URL,
  fetchProducts,
  fetchCustomers,
  fetchPosRecommendations,
  processPosCheckout,
  scanBarcodeApi,
} from '@/lib/api';
import {
  Search,
  Barcode,
  ShoppingBag,
  Trash2,
  Plus,
  Minus,
  CheckCircle,
  CreditCard,
  Banknote,
  QrCode,
  Sparkles,
  AlertTriangle,
  User,
  Receipt,
  RotateCcw,
  Zap,
  Radio,
} from 'lucide-react';

interface CartItem {
  product: PosProduct;
  batch: PosBatchSummary;
  quantity: number;
  appliedPromotion?: PosRecommendationItem;
  scannedBarcode?: string;
}

export default function PosPage() {
  const [products, setProducts] = useState<PosProduct[]>([]);
  const [customers, setCustomers] = useState<Customer[]>([]);
  const [selectedCustomerId, setSelectedCustomerId] = useState<number | undefined>(undefined);
  const [searchQuery, setSearchQuery] = useState('');
  const [selectedCategory, setSelectedCategory] = useState('ALL');
  const [cart, setCart] = useState<CartItem[]>([]);
  const [recommendations, setRecommendations] = useState<PosRecommendationItem[]>([]);
  const [isLoading, setIsLoading] = useState(true);

  // Barcode Scanning Toast Feedback
  const [scanAlert, setScanAlert] = useState<{
    type: 'success' | 'error' | 'warning';
    title: string;
    message: string;
  } | null>(null);

  // SignalR Real-time Promotion State
  const [isSignalRConnected, setIsSignalRConnected] = useState(false);
  const [realtimeNotification, setRealtimeNotification] = useState<{
    id: number;
    message: string;
    discountPercent: number;
    productName: string;
  } | null>(null);
  const [highlightedProductId, setHighlightedProductId] = useState<number | null>(null);

  // Checkout state
  const [isCheckoutOpen, setIsCheckoutOpen] = useState(false);
  const [paymentMethod, setPaymentMethod] = useState<'CASH' | 'CARD' | 'QR_CODE'>('CASH');
  const [receivedAmount, setReceivedAmount] = useState<number>(0);
  const [completedReceipt, setCompletedReceipt] = useState<any | null>(null);
  const [isProcessingCheckout, setIsProcessingCheckout] = useState(false);

  // Connect to SignalR PromotionHub for instant promotion notifications
  useEffect(() => {
    let isMounted = true;
    const connection = new signalR.HubConnectionBuilder()
      .withUrl(SIGNALR_HUB_URL, {
        skipNegotiation: false,
        transport: signalR.HttpTransportType.WebSockets | signalR.HttpTransportType.LongPolling,
      })
      .withAutomaticReconnect([0, 2000, 5000, 10000])
      .configureLogging(signalR.LogLevel.Information)
      .build();

    connection
      .start()
      .then(() => {
        if (isMounted) {
          console.log('[SignalR] Connected successfully to PromotionHub');
          setIsSignalRConnected(true);
        }
      })
      .catch((err) => {
        if (isMounted) {
          console.warn('[SignalR] Failed to connect initially (will auto-retry):', err);
          setIsSignalRConnected(false);
        }
      });

    connection.onclose(() => {
      if (isMounted) setIsSignalRConnected(false);
    });
    connection.onreconnecting(() => {
      if (isMounted) setIsSignalRConnected(false);
    });
    connection.onreconnected(() => {
      if (isMounted) setIsSignalRConnected(true);
    });

    connection.on('PromotionApproved', (data: {
      promotionId: number;
      promotionCode: string;
      promotionName: string;
      promotionType?: string;
      targetProductId?: number;
      targetProductName?: string;
      targetBatchId?: number;
      targetBatchCode?: string;
      discountPercent?: number;
      comboProductId?: number;
      comboProductName?: string;
      comboPrice?: number;
      comboSavings?: number;
      message: string;
    }) => {
      console.log('[SignalR] Received PromotionApproved event:', data);
      const isCombo = data.promotionType === 'BUNDLE_COMBO';
      const discountRate = data.discountPercent ?? 20;
      const prodName = data.targetProductName || '対象商品';
      const batchCodeStr = data.targetBatchCode ? ` [ロット: ${data.targetBatchCode}]` : '';

      // 1. Show dynamic notification banner
      setRealtimeNotification({
        id: data.promotionId,
        message: data.message || (isCombo
          ? `🍱 【新着コンボ】${prodName}＋${data.comboProductName ?? 'ドリンク'} セットが ¥${data.comboPrice} に承認されました！`
          : `【新着値引き】${prodName}${batchCodeStr} が ${discountRate}% OFF に承認されました！`),
        discountPercent: isCombo ? Math.round((data.comboSavings ?? 70) / 420 * 100) : discountRate,
        productName: isCombo ? `${prodName}＋${data.comboProductName ?? 'ドリンク'}` : `${prodName}${batchCodeStr}`,
      });

      // Dismiss notification banner automatically after 9 seconds
      setTimeout(() => {
        setRealtimeNotification((curr) => (curr?.id === data.promotionId ? null : curr));
      }, 9000);

      // 2. RULES 3 & 4: ONLY apply to cart items matching targetBatchId!
      if (data.targetBatchId) {
        const targetBatchId = data.targetBatchId;

        setCart((prevCart) => {
          const matchingItem = prevCart.find((i) => i.batch.id === targetBatchId);
          if (!matchingItem) return prevCart;

          // Highlight matching item in cart
          setHighlightedProductId(matchingItem.product.id);
          setTimeout(() => setHighlightedProductId(null), 3500);

          return prevCart.map((item) => {
            if (item.batch.id === targetBatchId) {
              const promoItem: PosRecommendationItem = {
                promotionId: data.promotionId,
                promotionCode: data.promotionCode,
                promotionName: data.promotionName,
                promotionType: 'DIRECT_DISCOUNT',
                targetProductId: item.product.id,
                targetProductName: prodName,
                targetBatchId: targetBatchId,
                targetBatchCode: data.targetBatchCode || item.batch.batchCode,
                originalPrice: item.product.price,
                discountPercent: discountRate,
                finalPrice: Math.round(item.product.price * (1 - discountRate / 100)),
                message: data.message,
                actionPrompt: '店長承認により自動適用されました',
              };
              return {
                ...item,
                appliedPromotion: promoItem,
              };
            }
            // Other batches of the same product maintain their own prices! (Rule 4)
            return item;
          });
        });
      }

      // Refresh product list to sync batch discount flags
      fetchProducts().then(setProducts).catch(console.error);
    });

    return () => {
      isMounted = false;
      connection.stop();
    };
  }, []);

  // Load initial products & customers
  useEffect(() => {
    loadInitialData();
  }, []);

  async function loadInitialData() {
    setIsLoading(true);
    try {
      const [prodData, custData] = await Promise.all([
        fetchProducts(),
        fetchCustomers(),
      ]);
      setProducts(prodData);
      setCustomers(custData);
    } catch (err) {
      console.error(err);
    } finally {
      setIsLoading(false);
    }
  }

  // Filter products by search & category
  const categories = ['ALL', 'お弁当 (Bento)', 'サラダ・総菜 (Salad & Delica)', 'サンドイッチ・パン (Bakery)', '飲料・乳製品 (Drinks & Dairy)'];
  const filteredProducts = products.filter((p) => {
    const matchCat = selectedCategory === 'ALL' || p.categoryName.includes(selectedCategory.split(' ')[0]);
    const matchSearch =
      !searchQuery ||
      p.name.toLowerCase().includes(searchQuery.toLowerCase()) ||
      p.barcode.includes(searchQuery) ||
      p.productCode.toLowerCase().includes(searchQuery.toLowerCase());
    return matchCat && matchSearch;
  });

  // Whenever cart changes, fetch ultra-fast recommendations (<500ms)
  useEffect(() => {
    const productIds = cart.map((i) => i.product.id);
    const batchIds = cart.map((i) => i.batch.id);
    const subtotal = cart.reduce((sum, item) => sum + item.product.price * item.quantity, 0);

    if (productIds.length === 0) {
      setRecommendations([]);
      return;
    }

    fetchPosRecommendations(productIds, batchIds, subtotal, selectedCustomerId)
      .then((recs) => {
        setRecommendations(recs);
      })
      .catch((err) => console.error('Failed to load POS recommendations', err));
  }, [cart.map((i) => `${i.product.id}-${i.batch.id}-${i.quantity}`).join(','), selectedCustomerId]);

  function addToCart(product: PosProduct, specificBatch?: PosBatchSummary, scannedBarcode?: string) {
    const validBatches = (product.batches || []).filter((b) => !b.isExpired && b.remainingQuantity > 0);

    if (validBatches.length === 0 && !specificBatch) {
      setScanAlert({
        type: 'error',
        title: '在庫切れ・販売不可',
        message: `「${product.name}」は有効なロット在庫がありません（完売または賞味期限切れ）。`,
      });
      setTimeout(() => setScanAlert(null), 4500);
      return;
    }

    const targetBatch = specificBatch || validBatches[0];
    if (!targetBatch) {
      setScanAlert({
        type: 'error',
        title: 'ロット未検出',
        message: `「${product.name}」の選択可能なロットがありません。`,
      });
      setTimeout(() => setScanAlert(null), 4500);
      return;
    }

    // RULE 7: Expired items CANNOT be sold! (賞味期限切れ販売禁止)
    if (targetBatch.isExpired) {
      setScanAlert({
        type: 'error',
        title: '⛔ 【販売不可】賞味期限切れ',
        message: `ロット「${targetBatch.batchCode}」は賞味期限が切れています！店頭から直ちに撤去してください（Rule 7: 期限切れ販売禁止）。`,
      });
      setTimeout(() => setScanAlert(null), 5000);
      return;
    }

    if (targetBatch.remainingQuantity <= 0) {
      setScanAlert({
        type: 'error',
        title: 'ロット完売',
        message: `ロット「${targetBatch.batchCode}」は在庫切れです。`,
      });
      setTimeout(() => setScanAlert(null), 4500);
      return;
    }

    const resolvedBarcode = scannedBarcode || product.barcode || targetBatch.batchCode;

    setCart((prev) => {
      const existing = prev.find((i) => i.product.id === product.id && i.batch.id === targetBatch.id);
      if (existing) {
        if (existing.quantity >= targetBatch.remainingQuantity) {
          setScanAlert({
            type: 'warning',
            title: 'ロット在庫上限',
            message: `ロット「${targetBatch.batchCode}」の在庫上限 (${targetBatch.remainingQuantity}個) を超えて追加することはできません。`,
          });
          setTimeout(() => setScanAlert(null), 4500);
          return prev;
        }
        setScanAlert({
          type: 'success',
          title: '数量追加',
          message: `「${product.name}」(ロット: ${targetBatch.batchCode}) を1点追加しました (計: ${existing.quantity + 1}点)。決済時にロット在庫から引落。`,
        });
        setTimeout(() => setScanAlert(null), 4500);
        return prev.map((i) =>
          i.product.id === product.id && i.batch.id === targetBatch.id ? { ...i, quantity: i.quantity + 1 } : i
        );
      }

      // If batch has an approved promotion, link it! (Rules 3, 6, 8)
      let defaultPromo: PosRecommendationItem | undefined = undefined;
      if (targetBatch.isDiscounted && targetBatch.promotionId && targetBatch.discountPercent) {
        defaultPromo = {
          promotionId: targetBatch.promotionId,
          promotionCode: `PROMO-BATCH-${targetBatch.id}`,
          promotionName: targetBatch.promotionName || `${targetBatch.discountPercent}% OFF`,
          promotionType: 'DIRECT_DISCOUNT',
          targetProductId: product.id,
          targetProductName: product.name,
          targetBatchId: targetBatch.id,
          targetBatchCode: targetBatch.batchCode,
          expiryDate: targetBatch.expiryDate,
          originalPrice: product.price,
          discountPercent: targetBatch.discountPercent,
          finalPrice: targetBatch.finalPrice || Math.round(product.price * (1 - targetBatch.discountPercent / 100)),
          message: `【値引き適用】${targetBatch.batchCode} が ${targetBatch.discountPercent}% OFF`,
          actionPrompt: '店長承認済み値引きシール貼付ロット',
        };
      }

      setScanAlert({
        type: 'success',
        title: '✅ バーコード読取・ロット確定',
        message: `「${product.name}」(ロット: ${targetBatch.batchCode}${defaultPromo ? ` • ${defaultPromo.discountPercent}% OFF` : ''}) をカゴに追加しました。決済時にロット在庫から直接引き落とされます。`,
      });
      setTimeout(() => setScanAlert(null), 4500);

      return [
        ...prev,
        {
          product,
          batch: targetBatch,
          quantity: 1,
          appliedPromotion: defaultPromo,
          scannedBarcode: resolvedBarcode,
        },
      ];
    });
  }

  function updateQuantity(productId: number, batchId: number, delta: number) {
    setCart((prev) =>
      prev
        .map((item) => {
          if (item.product.id === productId && item.batch.id === batchId) {
            const newQty = item.quantity + delta;
            if (delta > 0 && newQty > item.batch.remainingQuantity) {
              setScanAlert({
                type: 'warning',
                title: 'ロット在庫上限',
                message: `ロット「${item.batch.batchCode}」の在庫上限 (${item.batch.remainingQuantity}個) に達しています。`,
              });
              setTimeout(() => setScanAlert(null), 4500);
              return item;
            }
            return newQty > 0 ? { ...item, quantity: newQty } : null;
          }
          return item;
        })
        .filter(Boolean) as CartItem[]
    );
  }

  function removeFromCart(productId: number, batchId: number) {
    setCart((prev) => prev.filter((i) => !(i.product.id === productId && i.batch.id === batchId)));
  }

  function clearCart() {
    setCart([]);
    setRecommendations([]);
  }

  // Dynamic Barcode Scanner: handles JAN, batch codes, sticker codes, and server fallback
  function scanBarcode(rawCode: string) {
    if (!rawCode || !rawCode.trim()) return;
    const code = rawCode.trim();

    // 1. Direct Batch Code Match across all products
    for (const p of products) {
      const b = p.batches?.find((batch) => batch.batchCode.toLowerCase() === code.toLowerCase());
      if (b) {
        addToCart(p, b, code);
        return;
      }
    }

    // 2. Discount Sticker & Special Pattern (e.g. STICKER-SAND-001, EXPIRED-SAND, STICKER-BENTO-001)
    if (code.toUpperCase().includes('STICKER') || code.toUpperCase().includes('DISCOUNT')) {
      for (const p of products) {
        const b = p.batches?.find((batch) =>
          code.toLowerCase().includes(batch.batchCode.toLowerCase()) ||
          code.toLowerCase().includes(p.productCode.toLowerCase())
        );
        if (b) {
          addToCart(p, b, code);
          return;
        }
      }
    }

    if (code === 'EXPIRED-SAND') {
      const p = products.find((prod) => prod.productCode === 'SAND-001');
      const b = p?.batches?.find((batch) => batch.batchCode === 'BATCH-SAND-EXPIRED');
      if (p && b) {
        addToCart(p, b, code);
        return;
      }
    }

    // 3. Product JAN Barcode or Product Code Match
    const foundProduct = products.find(
      (p) => p.barcode === code || p.productCode.toLowerCase() === code.toLowerCase()
    );
    if (foundProduct) {
      // Preference: non-discounted regular batch (or earliest non-expired batch)
      const normalBatch =
        foundProduct.batches?.find((b) => !b.isExpired && !b.isDiscounted && b.remainingQuantity > 0) ||
        foundProduct.batches?.find((b) => !b.isExpired && b.remainingQuantity > 0);

      if (normalBatch) {
        addToCart(foundProduct, normalBatch, code);
      } else {
        setScanAlert({
          type: 'error',
          title: '販売不可',
          message: `「${foundProduct.name}」は有効なロット在庫がありません（完売または賞味期限切れ）。`,
        });
        setTimeout(() => setScanAlert(null), 4500);
      }
      return;
    }

    // 4. Server-side API fallback
    scanBarcodeApi(code)
      .then((res) => {
        if (res.success && res.productId && res.batchId) {
          const prod = products.find((p) => p.id === res.productId);
          const batch = prod?.batches?.find((b) => b.id === res.batchId);
          if (prod && batch) {
            addToCart(prod, batch, code);
            return;
          }
        }
        setScanAlert({
          type: 'error',
          title: '未登録バーコード',
          message: res.message || `バーコード「${code}」に一致する商品・ロットが見つかりませんでした。`,
        });
        setTimeout(() => setScanAlert(null), 4500);
      })
      .catch(() => {
        setScanAlert({
          type: 'error',
          title: '未登録バーコード',
          message: `バーコード「${code}」に一致する商品・ロットが見つかりませんでした。`,
        });
        setTimeout(() => setScanAlert(null), 4500);
      });
  }

  function handleScanInput() {
    if (!searchQuery.trim()) return;
    const code = searchQuery.trim();
    scanBarcode(code);
    setSearchQuery('');
  }

  // Apply recommendation by adding target product (or combo partner) to cart with promo
  function applyRecommendation(promo: PosRecommendationItem) {
    if (promo.promotionType === 'BUNDLE_COMBO' && promo.comboProductId) {
      const partnerProduct = products.find((p) => p.id === promo.comboProductId);
      if (partnerProduct) {
        const validBatch = (partnerProduct.batches || []).find((b) => !b.isExpired && b.remainingQuantity > 0);
        if (validBatch) {
          addToCart(partnerProduct, validBatch);
          setScanAlert({
            type: 'success',
            title: 'コンボ商品をカゴに追加',
            message: `「${partnerProduct.name}」をカゴに追加しました。ランチコンボ割引（-¥${promo.savingsAmount || 70}）が自動適用されます！`,
          });
          setTimeout(() => setScanAlert(null), 4000);
          return;
        }
      }
    }

    const productToAdd = products.find((p) => p.id === promo.targetProductId);
    if (!productToAdd) return;

    const targetBatch = (productToAdd.batches || []).find((b) => b.id === promo.targetBatchId);
    if (!targetBatch) {
      alert(`対象ロット (ID: ${promo.targetBatchId}) の在庫が見つかりません。`);
      return;
    }

    if (targetBatch.isExpired) {
      alert(`【販売不可】ロット「${targetBatch.batchCode}」は賞味期限が切れているため販売できません。`);
      return;
    }

    addToCart(productToAdd, targetBatch);
  }

  // Cart Calculations
  const subtotal = cart.reduce((sum, item) => sum + item.product.price * item.quantity, 0);

  // Check active combo discounts from recommendations
  const activeComboDiscount = recommendations.reduce((acc, r) => {
    if (r.promotionType === 'BUNDLE_COMBO_APPLIED') {
      return acc + (r.savingsAmount || 70);
    }
    return acc;
  }, 0);

  const discountTotal = cart.reduce((sum, item) => {
    if (item.appliedPromotion) {
      const discountRate = item.appliedPromotion.discountPercent / 100;
      return sum + Math.round(item.product.price * discountRate * item.quantity);
    }
    return sum;
  }, 0) + activeComboDiscount;

  const totalAmount = Math.max(0, subtotal - discountTotal);
  const changeAmount = Math.max(0, receivedAmount - totalAmount);

  // Handle Checkout submission
  async function handleCompletePayment() {
    if (cart.length === 0) return;

    // Front-end pre-validation for expired items and stock limits
    for (const item of cart) {
      if (item.batch.isExpired) {
        alert(`【販売不可】「${item.product.name}」(ロット: ${item.batch.batchCode}) は賞味期限が切れています！\nレジから削除し、店頭から撤去してください。`);
        return;
      }
      if (item.quantity > item.batch.remainingQuantity) {
        alert(`ロット「${item.batch.batchCode}」の注文数 (${item.quantity}個) が現在庫 (${item.batch.remainingQuantity}個) を超過しています。数量を修正してください。`);
        return;
      }
    }

    setIsProcessingCheckout(true);

    try {
      const payload = {
        customerId: selectedCustomerId,
        paymentMethod: paymentMethod,
        receivedAmount: paymentMethod === 'CASH' ? receivedAmount : totalAmount,
        items: cart.map((i) => ({
          productId: i.product.id,
          batchId: i.batch.id,
          batchCode: i.batch.batchCode,
          scannedBarcode: i.scannedBarcode || i.product.barcode || i.batch.batchCode,
          quantity: i.quantity,
          appliedPromotionId: i.appliedPromotion?.promotionId,
        })),
      };

      const result = await processPosCheckout(payload);
      setCompletedReceipt(result);
      setCart([]);
      setRecommendations([]);
      setIsCheckoutOpen(false);

      // Refresh product stock across all batches
      const updatedProds = await fetchProducts();
      setProducts(updatedProds);
    } catch (err: any) {
      alert(err.message || 'チェックアウト処理に失敗しました。');
      console.error(err);
    } finally {
      setIsProcessingCheckout(false);
    }
  }

  return (
    <div className="min-h-screen flex flex-col bg-slate-100">
      <Navbar />

      <div className="flex-1 max-w-[1600px] w-full mx-auto p-4 flex flex-col lg:flex-row gap-5">
        {/* Left Side: Product Catalogue & Scanning */}
        <div className="flex-1 flex flex-col bg-white rounded-2xl border border-slate-200 shadow-sm overflow-hidden">
          {/* Barcode & Search Bar */}
          <div className="p-4 border-b border-slate-100 bg-slate-50 space-y-3">
            <div className="flex gap-2">
              <div className="relative flex-1">
                <Search className="w-5 h-5 absolute left-3.5 top-3 text-slate-400" />
                <input
                  type="text"
                  placeholder="JANコード、ロット番号 (BATCH-...)、型番を入力またはバーコードスキャン (Enterで登録)..."
                  value={searchQuery}
                  onChange={(e) => setSearchQuery(e.target.value)}
                  onKeyDown={(e) => {
                    if (e.key === 'Enter') {
                      e.preventDefault();
                      handleScanInput();
                    }
                  }}
                  className="w-full pl-10 pr-4 py-2.5 bg-white rounded-xl border border-slate-300 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500 shadow-inner"
                />
              </div>
              <button
                onClick={handleScanInput}
                disabled={!searchQuery.trim()}
                className="px-4 py-2.5 bg-emerald-600 hover:bg-emerald-700 disabled:bg-slate-300 text-white font-bold text-xs rounded-xl shadow transition flex items-center gap-1.5 shrink-0 active:scale-95"
              >
                <Barcode className="w-4 h-4" />
                <span>スキャン実行</span>
              </button>
            </div>

            {/* Real-time Scan Notification Alert */}
            {scanAlert && (
              <div
                className={`p-3 rounded-xl border text-xs flex items-center justify-between gap-2 shadow-sm transition animate-in fade-in slide-in-from-top-2 ${
                  scanAlert.type === 'error'
                    ? 'bg-rose-50 border-rose-300 text-rose-900'
                    : scanAlert.type === 'warning'
                    ? 'bg-amber-50 border-amber-300 text-amber-900'
                    : 'bg-emerald-50 border-emerald-300 text-emerald-900'
                }`}
              >
                <div className="flex items-center gap-2">
                  {scanAlert.type === 'error' ? (
                    <AlertTriangle className="w-4 h-4 text-rose-600 shrink-0" />
                  ) : scanAlert.type === 'warning' ? (
                    <AlertTriangle className="w-4 h-4 text-amber-600 shrink-0" />
                  ) : (
                    <CheckCircle className="w-4 h-4 text-emerald-600 shrink-0" />
                  )}
                  <div>
                    <span className="font-bold block">{scanAlert.title}</span>
                    <span className="text-[11px] opacity-90">{scanAlert.message}</span>
                  </div>
                </div>
                <button
                  onClick={() => setScanAlert(null)}
                  className="text-slate-400 hover:text-slate-600 text-xs px-1.5 py-0.5 rounded"
                >
                  ✕
                </button>
              </div>
            )}


            {/* Category Filter Pills */}
            <div className="flex overflow-x-auto gap-1.5 pt-1 text-xs">
              {categories.map((cat) => {
                const label = cat === 'ALL' ? 'すべて' : cat.split(' ')[0];
                return (
                  <button
                    key={cat}
                    onClick={() => setSelectedCategory(cat)}
                    className={`px-3 py-1.5 rounded-lg font-bold transition whitespace-nowrap ${
                      selectedCategory === cat
                        ? 'bg-slate-900 text-white shadow'
                        : 'bg-white text-slate-600 border border-slate-200 hover:bg-slate-100'
                    }`}
                  >
                    {label}
                  </button>
                );
              })}
            </div>
          </div>

          {/* Product Grid */}
          <div className="flex-1 p-4 overflow-y-auto max-h-[calc(100vh-280px)]">
            {isLoading ? (
              <div className="text-center py-20 text-slate-400">商品データを読み込み中...</div>
            ) : filteredProducts.length === 0 ? (
              <div className="text-center py-20 text-slate-400">該当する商品が見つかりません</div>
            ) : (
              <div className="grid grid-cols-2 sm:grid-cols-3 xl:grid-cols-4 gap-3.5">
                {filteredProducts.map((p) => {
                  const isSoldOut = p.totalAvailableStock <= 0;
                  const hasUrgentStock = !isSoldOut && p.earliestExpiryFormatted.includes('残り');
                  return (
                    <div
                      key={p.id}
                      onClick={() => !isSoldOut && addToCart(p)}
                      className={`group bg-white border rounded-xl p-3.5 flex flex-col justify-between transition shadow-sm relative overflow-hidden ${
                        isSoldOut
                          ? 'border-slate-200 opacity-60 cursor-not-allowed bg-slate-50'
                          : 'border-slate-200 hover:border-emerald-500 cursor-pointer hover:shadow-md'
                      }`}
                    >
                      {isSoldOut ? (
                        <div className="absolute top-2 right-2 bg-rose-600 text-white font-black text-[10px] px-2 py-0.5 rounded-full shadow">
                          完売 (SOLD OUT)
                        </div>
                      ) : hasUrgentStock ? (
                        <div className="absolute top-2 right-2 bg-amber-100 text-amber-900 font-bold text-[10px] px-2 py-0.5 rounded-full border border-amber-300">
                          {p.earliestExpiryFormatted.split(' ')[1] || '期限間近'}
                        </div>
                      ) : null}

                      <div>
                        <span className="text-[10px] font-bold text-slate-400 uppercase tracking-wider block">
                          {p.categoryName.split(' ')[0]}
                        </span>
                        <h3 className={`font-bold text-sm line-clamp-2 mt-0.5 transition-colors ${
                          isSoldOut ? 'text-slate-400' : 'text-slate-900 group-hover:text-emerald-600'
                        }`}>
                          {p.name}
                        </h3>
                        <p className="text-[11px] text-slate-500 mt-1 line-clamp-1">{p.description}</p>
                      </div>

                      {/* Batches Breakdown */}
                      {p.batches && p.batches.length > 0 && (
                        <div className="mt-2 pt-2 border-t border-slate-100 space-y-1" onClick={(e) => e.stopPropagation()}>
                          <div className="text-[10px] font-bold text-slate-400 flex items-center justify-between">
                            <span>ロット一覧 ({p.batches.length}):</span>
                          </div>
                          <div className="space-y-1 max-h-32 overflow-y-auto pr-0.5">
                            {p.batches.map((b) => {
                              if (b.isExpired) {
                                return (
                                  <div
                                    key={b.id}
                                    onClick={() => alert(`⛔ 【販売不可】ロット「${b.batchCode}」は賞味期限が切れています！\n店頭から直ちに撤去してください（Rule 7: 期限切れ販売禁止）。`)}
                                    className="p-1 px-1.5 rounded-md bg-rose-50 border border-rose-200 text-[10px] flex items-center justify-between text-rose-700 opacity-70 hover:opacity-100 cursor-pointer"
                                    title="Rule 7: 賞味期限切れ販売不可"
                                  >
                                    <span className="font-bold truncate">{b.batchCode} (期限切れ)</span>
                                    <span className="text-[9px] font-black bg-rose-200 text-rose-800 px-1 rounded shrink-0">
                                      販売不可
                                    </span>
                                  </div>
                                );
                              }

                              if (b.isDiscounted) {
                                return (
                                  <div
                                    key={b.id}
                                    onClick={() => addToCart(p, b)}
                                    className="p-1 px-1.5 rounded-md bg-amber-50 hover:bg-amber-100 border border-amber-300 text-[10px] flex items-center justify-between cursor-pointer transition shadow-xs"
                                    title="値引きシール貼付ロットを追加"
                                  >
                                    <div className="truncate pr-1">
                                      <div className="flex items-center gap-1">
                                        <span className="bg-amber-400 text-slate-950 font-black px-1 rounded text-[8px]">
                                          {b.discountPercent}% OFF
                                        </span>
                                        <span className="font-bold text-slate-900 truncate">{b.batchCode}</span>
                                      </div>
                                      <span className="text-[9px] text-amber-800 font-semibold block">
                                        ¥{b.finalPrice?.toLocaleString()} (残{b.remainingQuantity}個・{b.hoursUntilExpiry}h)
                                      </span>
                                    </div>
                                    <span className="w-5 h-5 rounded bg-amber-500 hover:bg-amber-600 text-slate-950 font-bold text-xs flex items-center justify-center shrink-0">
                                      +
                                    </span>
                                  </div>
                                );
                              }

                              // Regular fresh batch
                              return (
                                <div
                                  key={b.id}
                                  onClick={() => addToCart(p, b)}
                                  className="p-1 px-1.5 rounded-md bg-slate-50 hover:bg-slate-100 border border-slate-200 text-[10px] flex items-center justify-between cursor-pointer transition"
                                  title="通常ロットを追加"
                                >
                                  <div className="truncate pr-1">
                                    <span className="font-bold text-slate-700 block truncate">{b.batchCode} (通常)</span>
                                    <span className="text-[9px] text-slate-500 block">
                                      ¥{p.price.toLocaleString()} (残{b.remainingQuantity}個・{b.hoursUntilExpiry}h)
                                    </span>
                                  </div>
                                  <span className="w-5 h-5 rounded bg-slate-200 hover:bg-emerald-500 hover:text-white text-slate-700 text-xs flex items-center justify-center shrink-0 transition">
                                    +
                                  </span>
                                </div>
                              );
                            })}
                          </div>
                        </div>
                      )}

                      <div className="mt-3 pt-2 border-t border-slate-100 flex items-center justify-between">
                        <div>
                          <span className="text-base font-black text-slate-900">¥{p.price.toLocaleString()}</span>
                          <span className={`text-[10px] block font-semibold ${isSoldOut ? 'text-rose-500 font-bold' : 'text-slate-400'}`}>
                            在庫: {p.totalAvailableStock}個
                          </span>
                        </div>
                        <button
                          disabled={isSoldOut}
                          className={`w-8 h-8 rounded-lg flex items-center justify-center transition shadow ${
                            isSoldOut
                              ? 'bg-slate-300 text-slate-400 cursor-not-allowed'
                              : 'bg-emerald-500 group-hover:bg-emerald-600 text-white'
                          }`}
                        >
                          <Plus className="w-4 h-4" />
                        </button>
                      </div>
                    </div>
                  );
                })}
              </div>
            )}
          </div>
        </div>

        {/* Right Side: Cart, Promotions & Payment */}
        <div className="w-full lg:w-[480px] flex flex-col gap-4">
          {/* Cart Card */}
          <div className="bg-white rounded-2xl border border-slate-200 shadow-sm flex flex-col flex-1 overflow-hidden">
            {/* Cart Header */}
            <div className="p-4 border-b border-slate-100 flex items-center justify-between bg-slate-900 text-white">
              <div className="flex items-center gap-2.5">
                <ShoppingBag className="w-5 h-5 text-emerald-400" />
                <h2 className="font-bold text-base">お買物カゴ ({cart.reduce((s, i) => s + i.quantity, 0)}点)</h2>
              </div>
              <div className="flex items-center gap-2">
                <div
                  className={`flex items-center gap-1.5 px-2.5 py-0.5 rounded-full text-[10px] font-bold border transition ${
                    isSignalRConnected
                      ? 'bg-emerald-500/20 text-emerald-300 border-emerald-400/40'
                      : 'bg-amber-500/20 text-amber-300 border-amber-400/40'
                  }`}
                  title={isSignalRConnected ? 'SignalR経由で店長端末とリアルタイム同期中' : 'オフライン・ローカル動作中'}
                >
                  <span className={`w-1.5 h-1.5 rounded-full ${isSignalRConnected ? 'bg-emerald-400 animate-ping' : 'bg-amber-400'}`} />
                  <span>{isSignalRConnected ? 'LIVE 連携中' : '同期準備中'}</span>
                </div>

                {cart.length > 0 && (
                  <button
                    onClick={clearCart}
                    className="text-xs text-slate-400 hover:text-rose-400 flex items-center gap-1 transition ml-1"
                  >
                    <Trash2 className="w-3.5 h-3.5" /> クリア
                  </button>
                )}
              </div>
            </div>

            {/* Real-time Notification Banner */}
            {realtimeNotification && (
              <div className="p-3 bg-gradient-to-r from-amber-500 via-emerald-600 to-teal-600 text-white shadow-md flex items-center justify-between gap-3 animate-in fade-in slide-in-from-top-3 duration-200">
                <div className="flex items-center gap-2 min-w-0">
                  <div className="w-7 h-7 rounded-full bg-white/20 flex items-center justify-center shrink-0 animate-bounce">
                    <Zap className="w-4 h-4 text-amber-300" />
                  </div>
                  <div className="truncate">
                    <div className="flex items-center gap-1.5">
                      <span className="bg-amber-400 text-slate-950 text-[9px] font-black px-1.5 py-0.2 rounded uppercase tracking-wider">
                        ⚡ リアルタイム反映
                      </span>
                      <span className="text-[11px] font-bold text-amber-200 truncate">
                        店長が特売を承認しました
                      </span>
                    </div>
                    <p className="text-xs font-black text-white truncate">
                      {realtimeNotification.productName} ➔ <span className="text-amber-300 underline font-black">{realtimeNotification.discountPercent}% OFF</span>
                    </p>
                  </div>
                </div>
                <button
                  onClick={() => setRealtimeNotification(null)}
                  className="text-xs text-white/80 hover:text-white px-2 py-1 bg-black/20 hover:bg-black/30 rounded shrink-0"
                >
                  ✕
                </button>
              </div>
            )}

            {/* Member Selection */}
            <div className="px-4 py-2.5 bg-slate-50 border-b border-slate-200 flex items-center justify-between text-xs">
              <span className="font-bold text-slate-700 flex items-center gap-1.5">
                <User className="w-3.5 h-3.5 text-slate-500" /> 会員選択:
              </span>
              <select
                value={selectedCustomerId || ''}
                onChange={(e) => setSelectedCustomerId(e.target.value ? Number(e.target.value) : undefined)}
                className="bg-white border border-slate-300 rounded-md px-2 py-1 text-xs text-slate-800 font-medium focus:outline-none focus:ring-1 focus:ring-emerald-500"
              >
                <option value="">一般のお客様（非会員）</option>
                {customers.map((c) => (
                  <option key={c.id} value={c.id}>
                    {c.customerCode} - {c.name} ({c.points} pt)
                  </option>
                ))}
              </select>
            </div>

            {/* AI 接客アシスト・レコメンデーション専用ウィジェット (AI Upsell Assistant Box) */}
            {recommendations.length > 0 && (
              <div className="p-3.5 bg-gradient-to-br from-slate-900 via-indigo-950 to-slate-900 text-white rounded-xl mx-3 mt-3 shadow-lg border border-indigo-500/30">
                <div className="flex items-center justify-between pb-2 mb-2.5 border-b border-white/10">
                  <div className="flex items-center gap-2">
                    <span className="w-6 h-6 rounded-full bg-amber-400 text-slate-950 flex items-center justify-center font-black text-xs shadow-xs animate-pulse">
                      💡
                    </span>
                    <div>
                      <h3 className="font-black text-xs text-white flex items-center gap-1.5">
                        <span>AI 接客アシスト</span>
                        <span className="bg-indigo-500/80 text-white text-[9px] font-bold px-1.5 py-0.2 rounded-full border border-indigo-400/40 uppercase">
                          リアルタイム提案
                        </span>
                      </h3>
                    </div>
                  </div>
                  <span className="text-[10px] text-indigo-300 font-mono font-bold">✨ AI Recommendation</span>
                </div>

                <div className="space-y-2.5">
                  {recommendations.map((promo) => {
                    const isCombo = promo.promotionType === 'BUNDLE_COMBO';
                    const isComboApplied = promo.promotionType === 'BUNDLE_COMBO_APPLIED';

                    if (isComboApplied) {
                      return (
                        <div
                          key={promo.promotionId}
                          className="bg-emerald-500/20 border border-emerald-400/40 rounded-xl p-2.5 text-xs text-emerald-200 flex items-center justify-between"
                        >
                          <div className="flex items-center gap-2">
                            <span className="text-lg">🎉</span>
                            <div>
                              <p className="font-black text-white">{promo.message}</p>
                              <p className="text-[10px] text-emerald-300">合計金額から ¥{(promo.savingsAmount || 70).toLocaleString()} 値引きされています</p>
                            </div>
                          </div>
                          <span className="bg-emerald-500 text-slate-950 text-[10px] font-black px-2 py-0.5 rounded-full">
                            適用中
                          </span>
                        </div>
                      );
                    }

                    return (
                      <div
                        key={promo.promotionId}
                        className="bg-white/5 hover:bg-white/10 border border-white/10 rounded-xl p-3 space-y-2 transition shadow-xs"
                      >
                        <div className="flex items-start justify-between gap-2">
                          <div className="flex items-center gap-1.5">
                            <span className="text-base">{isCombo ? '🥪🍵' : '🏷️'}</span>
                            <span className="font-black text-xs text-amber-300">
                              {isCombo ? 'ランチコンボの追加提案' : '値引きシールの適用案内'}
                            </span>
                            {isCombo && (
                              <span className="bg-amber-400 text-slate-950 font-black text-[9px] px-1.5 py-0.2 rounded">
                                ¥{(promo.savingsAmount || 70)} お得
                              </span>
                            )}
                          </div>
                        </div>

                        {/* Staff Script Speech Bubble (お客様へのご案内セリフ) */}
                        <div className="bg-amber-400/10 border border-amber-400/30 rounded-lg p-2 text-xs">
                          <div className="text-[10px] font-bold text-amber-300 flex items-center gap-1 mb-0.5">
                            <span>🗣️ お客様へのお声がけセリフ（レジ接客推奨）:</span>
                          </div>
                          <p className="text-white font-medium italic text-[11px] leading-relaxed">
                            {promo.staffScript || promo.actionPrompt}
                          </p>
                        </div>

                        <div className="flex items-center justify-between pt-1">
                          <div className="text-[10px] text-slate-300">
                            {isCombo ? (
                              <span>セット価格: <strong className="text-amber-300 text-sm font-black">¥{promo.comboPrice?.toLocaleString() || '350'}</strong> (通常: ¥{promo.originalPrice?.toLocaleString()})</span>
                            ) : (
                              <span>値引き後価格: <strong className="text-amber-300 text-sm font-black">¥{promo.finalPrice?.toLocaleString()}</strong></span>
                            )}
                          </div>

                          <button
                            onClick={() => applyRecommendation(promo)}
                            className="bg-gradient-to-r from-amber-400 to-amber-500 hover:from-amber-300 hover:to-amber-400 text-slate-950 font-black text-xs px-3 py-1.5 rounded-lg shadow-md flex items-center gap-1.5 transition active:scale-95 whitespace-nowrap"
                          >
                            <span>{isCombo ? `＋ ${promo.comboProductName || '宇治緑茶'}を追加` : 'おすすめする'}</span>
                          </button>
                        </div>
                      </div>
                    );
                  })}
                </div>
              </div>
            )}

            {/* Cart Items List */}
            <div className="flex-1 p-4 overflow-y-auto max-h-[360px] space-y-3">
              {cart.length === 0 ? (
                <div className="text-center py-16 text-slate-400 text-sm">
                  商品をスキャンまたはクリックして<br />カゴに追加してください
                </div>
              ) : (
                cart.map((item) => {
                  const lineSubtotal = item.product.price * item.quantity;
                  const discountRate = item.appliedPromotion ? item.appliedPromotion.discountPercent / 100 : 0;
                  const lineDiscount = Math.round(lineSubtotal * discountRate);
                  const lineFinal = lineSubtotal - lineDiscount;
                  const isHighlighted = item.product.id === highlightedProductId;

                  return (
                    <div
                      key={`${item.product.id}-${item.batch.id}`}
                      className={`p-3 rounded-xl border transition flex flex-col gap-2 ${
                        isHighlighted
                          ? 'border-amber-400 ring-2 ring-amber-300/80 bg-amber-50/70 shadow-md animate-pulse'
                          : 'border-slate-200 bg-white hover:border-slate-300'
                      }`}
                    >
                      <div className="flex justify-between items-start">
                        <div>
                          <div className="flex items-center gap-1.5 flex-wrap">
                            <h4 className="font-bold text-sm text-slate-900">{item.product.name}</h4>
                            <span className="text-[10px] font-mono font-bold bg-slate-100 text-slate-700 px-1.5 py-0.5 rounded border border-slate-200">
                              ロット: {item.batch.batchCode}
                            </span>
                            {item.scannedBarcode && (
                              <span className="text-[9px] font-mono font-bold bg-emerald-50 text-emerald-800 px-1.5 py-0.5 rounded border border-emerald-200" title="スキャンされたバーコード">
                                🏷️ {item.scannedBarcode}
                              </span>
                            )}
                            {isHighlighted && (
                              <span className="bg-amber-500 text-white text-[9px] font-black px-1.5 py-0.5 rounded shadow">
                                ⚡ 即時更新
                              </span>
                            )}
                          </div>
                          <div className="text-[11px] text-slate-500 mt-0.5 flex items-center gap-2">
                            <span>定価: ¥{item.product.price.toLocaleString()}</span>
                            <span>•</span>
                            <span className={item.batch.hoursUntilExpiry <= 6 ? 'text-amber-600 font-bold' : ''}>
                              賞味期限: {item.batch.expiryFormatted || new Date(item.batch.expiryDate).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}
                            </span>
                          </div>
                          {item.appliedPromotion ? (
                            <div className="mt-1 flex items-center gap-1.5 flex-wrap">
                              <span className="inline-flex items-center gap-1 text-[10px] font-bold bg-amber-100 text-amber-900 border border-amber-300 px-2 py-0.5 rounded-md">
                                <Sparkles className="w-3 h-3 text-amber-600" />
                                値引きシール適用: {item.appliedPromotion.promotionName || '特売'} ({item.appliedPromotion.discountPercent}% OFF)
                              </span>
                            </div>
                          ) : (
                            <div className="mt-1">
                              <span className="inline-flex items-center text-[10px] font-semibold bg-slate-100 text-slate-600 px-1.5 py-0.5 rounded">
                                通常ロット（定価販売）
                              </span>
                            </div>
                          )}
                        </div>
                        <div className="text-right">
                          <span className="font-black text-sm text-slate-900">¥{lineFinal.toLocaleString()}</span>
                          {lineDiscount > 0 && (
                            <span className="block text-[10px] text-rose-600 font-bold">
                              -¥{lineDiscount.toLocaleString()} ({item.appliedPromotion?.discountPercent}% OFF)
                            </span>
                          )}
                        </div>
                      </div>

                      {/* Quantity buttons */}
                      <div className="flex items-center justify-between pt-1 border-t border-slate-100">
                        <div className="flex items-center gap-2">
                          <button
                            onClick={() => updateQuantity(item.product.id, item.batch.id, -1)}
                            className="w-6 h-6 rounded bg-slate-100 hover:bg-slate-200 flex items-center justify-center text-slate-600 text-xs transition"
                          >
                            <Minus className="w-3 h-3" />
                          </button>
                          <span className="text-xs font-bold w-6 text-center">{item.quantity}</span>
                          <button
                            disabled={item.quantity >= item.batch.remainingQuantity}
                            onClick={() => updateQuantity(item.product.id, item.batch.id, 1)}
                            className="w-6 h-6 rounded bg-slate-100 hover:bg-slate-200 disabled:opacity-30 disabled:cursor-not-allowed flex items-center justify-center text-slate-600 text-xs transition"
                            title={item.quantity >= item.batch.remainingQuantity ? `ロット在庫上限 (${item.batch.remainingQuantity}個) に達しました` : '1点追加'}
                          >
                            <Plus className="w-3 h-3" />
                          </button>
                          {item.quantity >= item.batch.remainingQuantity && (
                            <span className="text-[10px] text-rose-500 font-bold ml-1">
                              (ロット在庫上限: {item.batch.remainingQuantity}個)
                            </span>
                          )}
                        </div>
                        <button
                          onClick={() => removeFromCart(item.product.id, item.batch.id)}
                          className="text-xs text-slate-400 hover:text-rose-500"
                        >
                          削除
                        </button>
                      </div>
                    </div>
                  );
                })
              )}
            </div>

            {/* Cart Summary & Checkout Trigger */}
            <div className="p-4 border-t border-slate-200 bg-slate-50 space-y-2.5">
              <div className="flex justify-between text-xs text-slate-600">
                <span>小計</span>
                <span className="font-bold">¥{subtotal.toLocaleString()}</span>
              </div>
              {discountTotal > 0 && (
                <div className="flex justify-between text-xs text-emerald-600 font-bold">
                  <span>プロモーション割引</span>
                  <span>-¥{discountTotal.toLocaleString()}</span>
                </div>
              )}
              <div className="flex justify-between items-baseline pt-2 border-t border-slate-200">
                <span className="font-extrabold text-sm text-slate-900">合計（税込）</span>
                <span className="font-black text-2xl text-slate-950">¥{totalAmount.toLocaleString()}</span>
              </div>

              <button
                disabled={cart.length === 0}
                onClick={() => {
                  setReceivedAmount(totalAmount);
                  setIsCheckoutOpen(true);
                }}
                className="w-full py-3.5 bg-emerald-600 hover:bg-emerald-500 disabled:bg-slate-300 text-white font-black text-base rounded-xl transition shadow-md flex items-center justify-center gap-2 mt-2"
              >
                <span>お会計へ進む (¥{totalAmount.toLocaleString()})</span>
              </button>
            </div>
          </div>
        </div>
      </div>

      {/* Checkout Modal */}
      {isCheckoutOpen && (
        <div className="fixed inset-0 bg-black/60 backdrop-blur-sm z-50 flex items-center justify-center p-4">
          <div className="bg-white w-full max-w-lg rounded-2xl shadow-2xl overflow-hidden animate-in fade-in zoom-in duration-150">
            <div className="p-5 bg-slate-900 text-white flex justify-between items-center">
              <div>
                <h3 className="font-black text-lg">お会計・決済処理</h3>
                <p className="text-xs text-slate-400 mt-0.5">ライフマート 新宿東口店</p>
              </div>
              <button
                onClick={() => setIsCheckoutOpen(false)}
                className="text-slate-400 hover:text-white text-sm"
              >
                ✕
              </button>
            </div>

            <div className="p-6 space-y-5">
              {/* Payment Method Selector */}
              <div>
                <label className="text-xs font-bold text-slate-600 uppercase tracking-wider block mb-2">
                  決済方法の選択
                </label>
                <div className="grid grid-cols-3 gap-2.5">
                  <button
                    onClick={() => setPaymentMethod('CASH')}
                    className={`p-3 rounded-xl border flex flex-col items-center gap-1.5 transition ${
                      paymentMethod === 'CASH'
                        ? 'border-emerald-500 bg-emerald-50 text-emerald-900 font-bold ring-2 ring-emerald-400'
                        : 'border-slate-200 hover:bg-slate-50 text-slate-600'
                    }`}
                  >
                    <Banknote className="w-5 h-5" />
                    <span className="text-xs">現金 (Cash)</span>
                  </button>
                  <button
                    onClick={() => setPaymentMethod('CARD')}
                    className={`p-3 rounded-xl border flex flex-col items-center gap-1.5 transition ${
                      paymentMethod === 'CARD'
                        ? 'border-emerald-500 bg-emerald-50 text-emerald-900 font-bold ring-2 ring-emerald-400'
                        : 'border-slate-200 hover:bg-slate-50 text-slate-600'
                    }`}
                  >
                    <CreditCard className="w-5 h-5" />
                    <span className="text-xs">クレジット (Card)</span>
                  </button>
                  <button
                    onClick={() => setPaymentMethod('QR_CODE')}
                    className={`p-3 rounded-xl border flex flex-col items-center gap-1.5 transition ${
                      paymentMethod === 'QR_CODE'
                        ? 'border-emerald-500 bg-emerald-50 text-emerald-900 font-bold ring-2 ring-emerald-400'
                        : 'border-slate-200 hover:bg-slate-50 text-slate-600'
                    }`}
                  >
                    <QrCode className="w-5 h-5" />
                    <span className="text-xs">PayPay / QR</span>
                  </button>
                </div>
              </div>

              {/* Total Due Amount */}
              <div className="bg-slate-100 p-4 rounded-xl flex justify-between items-center">
                <span className="font-bold text-slate-700 text-sm">請求金額</span>
                <span className="font-black text-2xl text-slate-900">¥{totalAmount.toLocaleString()}</span>
              </div>

              {/* Cash handling if cash */}
              {paymentMethod === 'CASH' && (
                <div className="space-y-3">
                  <label className="text-xs font-bold text-slate-600 uppercase tracking-wider block">
                    お預かり金額
                  </label>
                  <input
                    type="number"
                    value={receivedAmount || ''}
                    onChange={(e) => setReceivedAmount(Number(e.target.value))}
                    className="w-full px-4 py-2.5 border border-slate-300 rounded-xl text-lg font-black text-right focus:outline-none focus:ring-2 focus:ring-emerald-500"
                  />
                  {/* Quick Cash Buttons */}
                  <div className="flex gap-2">
                    <button
                      onClick={() => setReceivedAmount(totalAmount)}
                      className="flex-1 py-1.5 bg-slate-200 hover:bg-slate-300 rounded-lg text-xs font-bold text-slate-700"
                    >
                      ちょうど
                    </button>
                    <button
                      onClick={() => setReceivedAmount(1000)}
                      className="flex-1 py-1.5 bg-slate-200 hover:bg-slate-300 rounded-lg text-xs font-bold text-slate-700"
                    >
                      ¥1,000
                    </button>
                    <button
                      onClick={() => setReceivedAmount(5000)}
                      className="flex-1 py-1.5 bg-slate-200 hover:bg-slate-300 rounded-lg text-xs font-bold text-slate-700"
                    >
                      ¥5,000
                    </button>
                    <button
                      onClick={() => setReceivedAmount(10000)}
                      className="flex-1 py-1.5 bg-slate-200 hover:bg-slate-300 rounded-lg text-xs font-bold text-slate-700"
                    >
                      ¥10,000
                    </button>
                  </div>

                  {/* Change display */}
                  <div className="bg-emerald-50 border border-emerald-200 p-3 rounded-xl flex justify-between items-center text-emerald-900">
                    <span className="font-bold text-sm">お釣り</span>
                    <span className="font-black text-xl">¥{changeAmount.toLocaleString()}</span>
                  </div>
                </div>
              )}

              {/* Confirm Checkout Button */}
              <button
                disabled={isProcessingCheckout || (paymentMethod === 'CASH' && receivedAmount < totalAmount)}
                onClick={handleCompletePayment}
                className="w-full py-4 bg-emerald-600 hover:bg-emerald-500 disabled:bg-slate-300 text-white font-black text-lg rounded-xl shadow-lg transition flex items-center justify-center gap-2"
              >
                {isProcessingCheckout ? (
                  <span>処理中...</span>
                ) : (
                  <>
                    <CheckCircle className="w-5 h-5" />
                    <span>決済完了 ＆ レシート発行</span>
                  </>
                )}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Simulated Thermal Receipt Modal */}
      {completedReceipt && (
        <div className="fixed inset-0 bg-black/60 backdrop-blur-sm z-50 flex items-center justify-center p-4">
          <div className="bg-white w-full max-w-sm rounded-2xl shadow-2xl p-6 font-mono text-xs text-slate-800 space-y-4 animate-in fade-in zoom-in">
            <div className="text-center border-b border-dashed border-slate-300 pb-3">
              <h2 className="font-black text-base text-slate-950 font-sans">ライフマート 新宿東口店</h2>
              <p className="text-[10px] text-slate-500 mt-0.5">東京都新宿区西新宿1-1-1 / TEL: 03-1234-5678</p>
              <p className="text-[10px] text-slate-400 mt-1">取引番号: {completedReceipt.transactionNumber}</p>
              <p className="text-[10px] text-slate-400">{new Date(completedReceipt.createdAt).toLocaleString('ja-JP')}</p>
            </div>

            {/* Receipt Items */}
            <div className="space-y-1.5 border-b border-dashed border-slate-300 pb-3">
              {completedReceipt.items.map((item: any, idx: number) => (
                <div key={idx} className="flex justify-between items-start">
                  <div>
                    <span className="font-bold">{item.productName}</span>
                    <span className="block text-[10px] text-slate-500">
                      JAN: {item.barcode || 'N/A'} | ロット: {item.batchCode || '通常'} | ¥{item.unitPrice} × {item.quantity}点
                    </span>
                  </div>
                  <div className="text-right">
                    <span>¥{item.finalPrice.toLocaleString()}</span>
                    {item.discountAmount > 0 && (
                      <span className="block text-[10px] text-emerald-600 font-bold">
                        -¥{item.discountAmount.toLocaleString()}
                      </span>
                    )}
                  </div>
                </div>
              ))}
            </div>

            {/* Totals */}
            <div className="space-y-1 border-b border-dashed border-slate-300 pb-3">
              <div className="flex justify-between">
                <span>小計</span>
                <span>¥{completedReceipt.subtotal.toLocaleString()}</span>
              </div>
              {completedReceipt.discountAmount > 0 && (
                <div className="flex justify-between text-emerald-600 font-bold">
                  <span>値引合計</span>
                  <span>-¥{completedReceipt.discountAmount.toLocaleString()}</span>
                </div>
              )}
              <div className="flex justify-between text-sm font-black pt-1">
                <span>合計（税込）</span>
                <span>¥{completedReceipt.totalAmount.toLocaleString()}</span>
              </div>
              <div className="flex justify-between text-slate-500">
                <span>お預かり</span>
                <span>¥{(completedReceipt.totalAmount + completedReceipt.changeAmount).toLocaleString()}</span>
              </div>
              <div className="flex justify-between font-bold">
                <span>お釣り</span>
                <span>¥{completedReceipt.changeAmount.toLocaleString()}</span>
              </div>
            </div>

            <div className="text-center pt-2 text-[10px] text-slate-500 border-t border-dashed border-slate-200">
              <p>お買い上げありがとうございます。</p>
              <p className="text-emerald-700 font-bold mt-0.5">
                ✓ スキャンされたバーコード情報に基づき、対象ロット在庫から正確に引き落とされました。
              </p>
            </div>

            <button
              onClick={() => setCompletedReceipt(null)}
              className="w-full py-2.5 bg-slate-900 text-white rounded-xl font-bold font-sans text-xs hover:bg-slate-800 transition"
            >
              閉じる（次の接客へ）
            </button>
          </div>
        </div>
      )}
    </div>
  );
}

