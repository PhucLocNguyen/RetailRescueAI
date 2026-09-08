'use client';

import { useState, useEffect } from 'react';
import Navbar from '@/components/Navbar';
import {
  PosProduct,
  PosRecommendationItem,
  Customer,
  fetchProducts,
  fetchCustomers,
  fetchPosRecommendations,
  processPosCheckout,
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
} from 'lucide-react';

interface CartItem {
  product: PosProduct;
  quantity: number;
  appliedPromotion?: PosRecommendationItem;
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

  // Checkout state
  const [isCheckoutOpen, setIsCheckoutOpen] = useState(false);
  const [paymentMethod, setPaymentMethod] = useState<'CASH' | 'CARD' | 'QR_CODE'>('CASH');
  const [receivedAmount, setReceivedAmount] = useState<number>(0);
  const [completedReceipt, setCompletedReceipt] = useState<any | null>(null);
  const [isProcessingCheckout, setIsProcessingCheckout] = useState(false);

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
    const subtotal = cart.reduce((sum, item) => sum + item.product.price * item.quantity, 0);

    if (productIds.length === 0) {
      setRecommendations([]);
      return;
    }

    fetchPosRecommendations(productIds, subtotal, selectedCustomerId)
      .then((recs) => {
        setRecommendations(recs);
        // Automatically link promotions to cart items if matched
        setCart((prev) =>
          prev.map((item) => {
            const matchedPromo = recs.find((r) => r.targetProductId === item.product.id);
            return {
              ...item,
              appliedPromotion: matchedPromo || item.appliedPromotion,
            };
          })
        );
      })
      .catch((err) => console.error('Failed to load POS recommendations', err));
  }, [cart.map((i) => `${i.product.id}-${i.quantity}`).join(','), selectedCustomerId]);

  function addToCart(product: PosProduct) {
    setCart((prev) => {
      const existing = prev.find((i) => i.product.id === product.id);
      if (existing) {
        return prev.map((i) =>
          i.product.id === product.id ? { ...i, quantity: i.quantity + 1 } : i
        );
      }
      return [...prev, { product, quantity: 1 }];
    });
  }

  function updateQuantity(productId: number, delta: number) {
    setCart((prev) =>
      prev
        .map((item) => {
          if (item.product.id === productId) {
            const newQty = item.quantity + delta;
            return newQty > 0 ? { ...item, quantity: newQty } : null;
          }
          return item;
        })
        .filter(Boolean) as CartItem[]
    );
  }

  function removeFromCart(productId: number) {
    setCart((prev) => prev.filter((i) => i.product.id !== productId));
  }

  function clearCart() {
    setCart([]);
    setRecommendations([]);
  }

  // Quick Barcode Scan simulation
  function scanBarcode(barcode: string) {
    const found = products.find((p) => p.barcode === barcode);
    if (found) {
      addToCart(found);
    }
  }

  // Apply recommendation by adding target product to cart with promo
  function applyRecommendation(promo: PosRecommendationItem) {
    const productToAdd = products.find((p) => p.id === promo.targetProductId);
    if (productToAdd) {
      setCart((prev) => {
        const existing = prev.find((i) => i.product.id === productToAdd.id);
        if (existing) {
          return prev.map((i) =>
            i.product.id === productToAdd.id
              ? { ...i, quantity: i.quantity + 1, appliedPromotion: promo }
              : i
          );
        }
        return [...prev, { product: productToAdd, quantity: 1, appliedPromotion: promo }];
      });
    }
  }

  // Cart Calculations
  const subtotal = cart.reduce((sum, item) => sum + item.product.price * item.quantity, 0);

  const discountTotal = cart.reduce((sum, item) => {
    if (item.appliedPromotion) {
      const discountRate = item.appliedPromotion.discountPercent / 100;
      return sum + Math.round(item.product.price * discountRate * item.quantity);
    }
    return sum;
  }, 0);

  const totalAmount = Math.max(0, subtotal - discountTotal);
  const changeAmount = Math.max(0, receivedAmount - totalAmount);

  // Handle Checkout submission
  async function handleCompletePayment() {
    if (cart.length === 0) return;
    setIsProcessingCheckout(true);

    try {
      const payload = {
        customerId: selectedCustomerId,
        paymentMethod: paymentMethod,
        receivedAmount: paymentMethod === 'CASH' ? receivedAmount : totalAmount,
        items: cart.map((i) => ({
          productId: i.product.id,
          quantity: i.quantity,
          appliedPromotionId: i.appliedPromotion?.promotionId,
        })),
      };

      const result = await processPosCheckout(payload);
      setCompletedReceipt(result);
      setCart([]);
      setRecommendations([]);
      setIsCheckoutOpen(false);

      // Refresh product stock
      const updatedProds = await fetchProducts();
      setProducts(updatedProds);
    } catch (err) {
      alert('チェックアウト処理に失敗しました。');
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
                  placeholder="商品名、JANバーコード、型番で検索..."
                  value={searchQuery}
                  onChange={(e) => setSearchQuery(e.target.value)}
                  className="w-full pl-10 pr-4 py-2.5 bg-white rounded-xl border border-slate-300 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500 shadow-inner"
                />
              </div>
            </div>

            {/* Fast Barcode Simulator Buttons */}
            <div className="flex flex-wrap items-center gap-1.5 text-xs text-slate-500">
              <span className="font-bold flex items-center gap-1 text-slate-700 mr-1">
                <Barcode className="w-4 h-4 text-emerald-600" /> バーコード即時スキャン:
              </span>
              <button
                onClick={() => scanBarcode('4901234567011')}
                className="bg-emerald-50 hover:bg-emerald-100 text-emerald-800 border border-emerald-300 px-2.5 py-1 rounded-md font-semibold transition"
              >
                チキン南蛮弁当 (¥550)
              </button>
              <button
                onClick={() => scanBarcode('4901234567028')}
                className="bg-emerald-50 hover:bg-emerald-100 text-emerald-800 border border-emerald-300 px-2.5 py-1 rounded-md font-semibold transition"
              >
                サーモンサラダ (¥240)
              </button>
              <button
                onClick={() => scanBarcode('4901234567035')}
                className="bg-emerald-50 hover:bg-emerald-100 text-emerald-800 border border-emerald-300 px-2.5 py-1 rounded-md font-semibold transition"
              >
                たまごサンド (¥280)
              </button>
              <button
                onClick={() => scanBarcode('4901234567042')}
                className="bg-emerald-50 hover:bg-emerald-100 text-emerald-800 border border-emerald-300 px-2.5 py-1 rounded-md font-semibold transition"
              >
                宇治緑茶 (¥140)
              </button>
            </div>

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
                  const hasUrgentStock = p.earliestExpiryFormatted.includes('残り');
                  return (
                    <div
                      key={p.id}
                      onClick={() => addToCart(p)}
                      className="group bg-white border border-slate-200 hover:border-emerald-500 rounded-xl p-3.5 flex flex-col justify-between cursor-pointer transition shadow-sm hover:shadow-md relative overflow-hidden"
                    >
                      {hasUrgentStock && (
                        <div className="absolute top-2 right-2 bg-amber-100 text-amber-900 font-bold text-[10px] px-2 py-0.5 rounded-full border border-amber-300">
                          {p.earliestExpiryFormatted.split(' ')[1] || '期限間近'}
                        </div>
                      )}

                      <div>
                        <span className="text-[10px] font-bold text-slate-400 uppercase tracking-wider block">
                          {p.categoryName.split(' ')[0]}
                        </span>
                        <h3 className="font-bold text-sm text-slate-900 line-clamp-2 mt-0.5 group-hover:text-emerald-600 transition-colors">
                          {p.name}
                        </h3>
                        <p className="text-[11px] text-slate-500 mt-1 line-clamp-1">{p.description}</p>
                      </div>

                      <div className="mt-4 pt-2 border-t border-slate-100 flex items-center justify-between">
                        <div>
                          <span className="text-base font-black text-slate-900">¥{p.price.toLocaleString()}</span>
                          <span className="text-[10px] text-slate-400 block">在庫: {p.totalAvailableStock}個</span>
                        </div>
                        <button className="w-8 h-8 rounded-lg bg-emerald-500 group-hover:bg-emerald-600 text-white flex items-center justify-center transition shadow">
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
              <div className="flex items-center gap-2">
                <ShoppingBag className="w-5 h-5 text-emerald-400" />
                <h2 className="font-bold text-base">お買物カゴ ({cart.reduce((s, i) => s + i.quantity, 0)}点)</h2>
              </div>
              {cart.length > 0 && (
                <button
                  onClick={clearCart}
                  className="text-xs text-slate-400 hover:text-rose-400 flex items-center gap-1 transition"
                >
                  <Trash2 className="w-3.5 h-3.5" /> クリア
                </button>
              )}
            </div>

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

            {/* Smart Promotion Alerts (Real-time Recommendation < 500ms) */}
            {recommendations.length > 0 && (
              <div className="p-3 bg-gradient-to-r from-emerald-500 to-teal-600 text-white shadow-inner">
                <div className="flex items-center gap-1.5 text-xs font-bold uppercase tracking-wider mb-1.5">
                  <Sparkles className="w-4 h-4 text-amber-300 animate-spin" />
                  <span>AIおすすめプロモーション (承認済)</span>
                </div>
                <div className="space-y-2">
                  {recommendations.map((promo) => (
                    <div
                      key={promo.promotionId}
                      className="bg-white/10 backdrop-blur-sm p-2.5 rounded-xl border border-white/20 flex items-center justify-between"
                    >
                      <div className="pr-2">
                        <p className="font-black text-xs text-amber-200">{promo.message}</p>
                        <p className="text-[11px] text-white/90 italic mt-0.5">{promo.actionPrompt}</p>
                      </div>
                      <button
                        onClick={() => applyRecommendation(promo)}
                        className="bg-amber-400 hover:bg-amber-300 text-slate-950 font-black text-xs px-3 py-1.5 rounded-lg whitespace-nowrap shadow transition active:scale-95"
                      >
                        おすすめする
                      </button>
                    </div>
                  ))}
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

                  return (
                    <div
                      key={item.product.id}
                      className="p-3 rounded-xl border border-slate-200 bg-white hover:border-slate-300 transition flex flex-col gap-2"
                    >
                      <div className="flex justify-between items-start">
                        <div>
                          <h4 className="font-bold text-sm text-slate-900">{item.product.name}</h4>
                          <span className="text-xs text-slate-500">単価: ¥{item.product.price.toLocaleString()}</span>
                        </div>
                        <div className="text-right">
                          <span className="font-black text-sm text-slate-900">¥{lineFinal.toLocaleString()}</span>
                          {lineDiscount > 0 && (
                            <span className="block text-[10px] text-emerald-600 font-bold">
                              -¥{lineDiscount.toLocaleString()} ({item.appliedPromotion?.discountPercent}% OFF)
                            </span>
                          )}
                        </div>
                      </div>

                      {/* Quantity buttons */}
                      <div className="flex items-center justify-between pt-1 border-t border-slate-100">
                        <div className="flex items-center gap-2">
                          <button
                            onClick={() => updateQuantity(item.product.id, -1)}
                            className="w-6 h-6 rounded bg-slate-100 hover:bg-slate-200 flex items-center justify-center text-slate-600 text-xs"
                          >
                            <Minus className="w-3 h-3" />
                          </button>
                          <span className="text-xs font-bold w-6 text-center">{item.quantity}</span>
                          <button
                            onClick={() => updateQuantity(item.product.id, 1)}
                            className="w-6 h-6 rounded bg-slate-100 hover:bg-slate-200 flex items-center justify-center text-slate-600 text-xs"
                          >
                            <Plus className="w-3 h-3" />
                          </button>
                        </div>
                        <button
                          onClick={() => removeFromCart(item.product.id)}
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
                      ¥{item.unitPrice} × {item.quantity}点
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

            <div className="text-center pt-2 text-[10px] text-slate-500">
              <p>お買い上げありがとうございます。</p>
              <p>ロット在庫が自動更新されました。</p>
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

