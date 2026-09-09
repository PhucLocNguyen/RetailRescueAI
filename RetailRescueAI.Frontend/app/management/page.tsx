'use client';

import { useState, useEffect } from 'react';
import Link from 'next/link';
import {
  DashboardStats,
  InventoryBatch,
  fetchDashboardStats,
  fetchBatches,
} from '@/lib/api';
import AiAgentWorkflowModal from '@/components/AiAgentWorkflowModal';
import {
  AlertTriangle,
  Sparkles,
  Flame,
  Clock,
  TrendingDown,
  CheckCircle2,
  Boxes,
  ArrowRight,
  RefreshCw,
  Coins,
  Play,
} from 'lucide-react';

export default function ManagementDashboardPage() {
  const [stats, setStats] = useState<DashboardStats | null>(null);
  const [urgentBatches, setUrgentBatches] = useState<InventoryBatch[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [isAiModalOpen, setIsAiModalOpen] = useState(false);
  const [lastUpdated, setLastUpdated] = useState<Date>(new Date());

  useEffect(() => {
    loadData();
  }, []);

  async function loadData() {
    setIsLoading(true);
    try {
      const [statsData, batchData] = await Promise.all([
        fetchDashboardStats(),
        fetchBatches(),
      ]);
      setStats(statsData);
      setUrgentBatches(
        batchData.filter((b) => b.status === 'CRITICAL' || b.status === 'AT_RISK')
      );
      setLastUpdated(new Date());
    } catch (err) {
      console.error(err);
    } finally {
      setIsLoading(false);
    }
  }

  return (
    <div className="space-y-6">
      {/* Page Header */}
      <div className="flex flex-col sm:flex-row justify-between items-start sm:items-center gap-4 bg-white p-6 rounded-2xl border border-slate-200 shadow-sm">
        <div>
          <h1 className="text-2xl font-black text-slate-900 tracking-tight flex items-center gap-2">
            店舗マネジメント・ダッシュボード
          </h1>
          <p className="text-xs text-slate-500 mt-1">
            ライフマート 新宿東口店 | 最終更新: {lastUpdated.toLocaleTimeString('ja-JP')}
          </p>
        </div>

        <div className="flex items-center gap-3">
          <button
            onClick={loadData}
            className="p-2.5 rounded-xl border border-slate-200 hover:bg-slate-50 text-slate-600 transition"
            title="再読み込み"
          >
            <RefreshCw className={`w-4 h-4 ${isLoading ? 'animate-spin' : ''}`} />
          </button>

          <button
            onClick={() => setIsAiModalOpen(true)}
            className="px-5 py-2.5 bg-gradient-to-r from-purple-600 via-indigo-600 to-violet-600 hover:from-purple-500 hover:to-violet-500 text-white font-black text-xs rounded-xl shadow-md hover:shadow-lg transition flex items-center gap-2 active:scale-95"
          >
            <Sparkles className="w-4 h-4 text-amber-300" />
            <span>AIパイプライン実演デモ (手動実行)</span>
          </button>
        </div>
      </div>

      {/* KPI Cards Grid */}
      <div className="grid grid-cols-2 lg:grid-cols-4 gap-4">
        {/* At-Risk Products */}
        <div className="bg-white p-5 rounded-2xl border border-slate-200 shadow-xs flex flex-col justify-between">
          <div className="flex justify-between items-start">
            <span className="text-xs font-bold text-slate-500">賞味期限間近商品</span>
            <div className="p-2 bg-amber-50 text-amber-600 rounded-lg">
              <Clock className="w-4 h-4" />
            </div>
          </div>
          <div className="mt-3">
            <div className="text-2xl font-black text-slate-900">
              {stats?.atRiskProductsCount || 0}
              <span className="text-xs font-normal text-slate-500 ml-1">品目</span>
            </div>
            <span className="text-[11px] text-amber-600 font-semibold">24時間以内切迫</span>
          </div>
        </div>

        {/* Critical Products */}
        <div className="bg-white p-5 rounded-2xl border border-slate-200 shadow-xs flex flex-col justify-between">
          <div className="flex justify-between items-start">
            <span className="text-xs font-bold text-slate-500">危機的商品 (CRITICAL)</span>
            <div className="p-2 bg-rose-50 text-rose-600 rounded-lg">
              <Flame className="w-4 h-4" />
            </div>
          </div>
          <div className="mt-3">
            <div className="text-2xl font-black text-rose-600">
              {stats?.criticalProductsCount || 0}
              <span className="text-xs font-normal text-slate-500 ml-1">品目</span>
            </div>
            <span className="text-[11px] text-rose-600 font-semibold">12時間以内切迫</span>
          </div>
        </div>

        {/* Potential Waste */}
        <div className="bg-white p-5 rounded-2xl border border-slate-200 shadow-xs flex flex-col justify-between">
          <div className="flex justify-between items-start">
            <span className="text-xs font-bold text-slate-500">潜在的廃棄損失</span>
            <div className="p-2 bg-red-50 text-red-600 rounded-lg">
              <Coins className="w-4 h-4" />
            </div>
          </div>
          <div className="mt-3">
            <div className="text-2xl font-black text-slate-900">
              ¥{(stats?.potentialWasteCost || 0).toLocaleString()}
            </div>
            <span className="text-[11px] text-slate-400">対策なしの場合の損失見込</span>
          </div>
        </div>

        {/* Pending AI Recommendations */}
        <div className="bg-white p-5 rounded-2xl border border-indigo-200 bg-indigo-50/20 shadow-xs flex flex-col justify-between">
          <div className="flex justify-between items-start">
            <span className="text-xs font-bold text-indigo-900">承認待ち AI提案</span>
            <div className="p-2 bg-indigo-100 text-indigo-600 rounded-lg">
              <Sparkles className="w-4 h-4" />
            </div>
          </div>
          <div className="mt-3">
            <div className="text-2xl font-black text-indigo-600">
              {stats?.pendingAiRecommendationsCount || 0}
              <span className="text-xs font-normal text-slate-500 ml-1">件</span>
            </div>
            <Link
              href="/management/ai-recommendations"
              className="text-[11px] text-indigo-600 font-bold hover:underline flex items-center gap-1 mt-0.5"
            >
              <span>審査・承認する</span>
              <ArrowRight className="w-3 h-3" />
            </Link>
          </div>
        </div>

        {/* Active Promotions */}
        <div className="bg-white p-5 rounded-2xl border border-slate-200 shadow-xs flex flex-col justify-between">
          <div className="flex justify-between items-start">
            <span className="text-xs font-bold text-slate-500">稼働中プロモーション</span>
            <div className="p-2 bg-emerald-50 text-emerald-600 rounded-lg">
              <CheckCircle2 className="w-4 h-4" />
            </div>
          </div>
          <div className="mt-3">
            <div className="text-2xl font-black text-emerald-600">
              {stats?.activePromotionsCount || 0}
              <span className="text-xs font-normal text-slate-500 ml-1">件</span>
            </div>
            <span className="text-[11px] text-slate-400">レジPOSで現在適用中</span>
          </div>
        </div>

        {/* Waste Reduction Rate */}
        <div className="bg-white p-5 rounded-2xl border border-slate-200 shadow-xs flex flex-col justify-between">
          <div className="flex justify-between items-start">
            <span className="text-xs font-bold text-slate-500">廃棄削減率（実績平均）</span>
            <div className="p-2 bg-emerald-50 text-emerald-600 rounded-lg">
              <TrendingDown className="w-4 h-4" />
            </div>
          </div>
          <div className="mt-3">
            <div className="text-2xl font-black text-emerald-600">
              {stats?.wasteReductionRate || 0}%
            </div>
            <span className="text-[11px] text-slate-400">AI販促導入による削減効果</span>
          </div>
        </div>

        {/* Recovered Revenue */}
        <div className="bg-white p-5 rounded-2xl border border-slate-200 shadow-xs flex flex-col justify-between">
          <div className="flex justify-between items-start">
            <span className="text-xs font-bold text-slate-500">回収収益（累計）</span>
            <div className="p-2 bg-blue-50 text-blue-600 rounded-lg">
              <Coins className="w-4 h-4" />
            </div>
          </div>
          <div className="mt-3">
            <div className="text-2xl font-black text-slate-900">
              ¥{(stats?.recoveredRevenueTotal || 0).toLocaleString()}
            </div>
            <span className="text-[11px] text-slate-400">廃棄危機からの売上転換</span>
          </div>
        </div>

        {/* Items Saved */}
        <div className="bg-white p-5 rounded-2xl border border-slate-200 shadow-xs flex flex-col justify-between">
          <div className="flex justify-between items-start">
            <span className="text-xs font-bold text-slate-500">救済商品点数</span>
            <div className="p-2 bg-teal-50 text-teal-600 rounded-lg">
              <Boxes className="w-4 h-4" />
            </div>
          </div>
          <div className="mt-3">
            <div className="text-2xl font-black text-slate-900">
              {stats?.totalItemsSaved || 0}
              <span className="text-xs font-normal text-slate-500 ml-1">点</span>
            </div>
            <span className="text-[11px] text-teal-600 font-semibold">食品ロス防止達成</span>
          </div>
        </div>
      </div>

      {/* Urgent Warning Hero Scenario (Chicken Bento Focus) */}
      <div className="bg-gradient-to-r from-amber-500 to-rose-500 text-white rounded-2xl p-6 shadow-md flex flex-col md:flex-row items-start md:items-center justify-between gap-5">
        <div className="space-y-1">
          <span className="bg-white/20 text-white font-bold text-xs px-2.5 py-1 rounded-full uppercase tracking-wider">
            優先対応アラート
          </span>
          <h2 className="text-xl font-black">
            チキン南蛮弁当 (BATCH-BENTO-001) の賞味期限が迫っています
          </h2>
          <p className="text-xs text-white/90 leading-relaxed max-w-2xl">
            現在庫 <strong>30個</strong> / 賞味期限まで残り <strong>約18時間</strong>。通常日販ペース（12個/日）では約21個の廃棄が見込まれます。
            AIエージェントによる「夕方直前割 20% OFF」の提案を審査してください。
          </p>
        </div>
        <Link
          href="/management/ai-recommendations"
          className="bg-white hover:bg-slate-100 text-slate-900 font-black text-sm px-5 py-3 rounded-xl whitespace-nowrap shadow-lg transition active:scale-95 flex items-center gap-2"
        >
          <span>AI提案を確認・承認</span>
          <ArrowRight className="w-4 h-4 text-indigo-600" />
        </Link>
      </div>

      {/* Urgent Batches Table */}
      <div className="bg-white rounded-2xl border border-slate-200 shadow-sm overflow-hidden">
        <div className="p-5 border-b border-slate-100 flex items-center justify-between">
          <div>
            <h3 className="font-bold text-base text-slate-900">注意・危機的ロット一覧</h3>
            <p className="text-xs text-slate-500 mt-0.5">賞味期限が24時間以内の対象ロット</p>
          </div>
          <Link
            href="/management/inventory"
            className="text-xs font-bold text-indigo-600 hover:underline flex items-center gap-1"
          >
            <span>全在庫を見る</span>
            <ArrowRight className="w-3.5 h-3.5" />
          </Link>
        </div>

        <div className="overflow-x-auto">
          <table className="w-full text-left text-xs">
            <thead className="bg-slate-50 text-slate-500 font-bold border-b border-slate-100">
              <tr>
                <th className="py-3 px-4">ロット番号</th>
                <th className="py-3 px-4">商品名</th>
                <th className="py-3 px-4">カテゴリ</th>
                <th className="py-3 px-4 text-right">定価</th>
                <th className="py-3 px-4 text-right">現在庫数</th>
                <th className="py-3 px-4">賞味期限</th>
                <th className="py-3 px-4">残存時間</th>
                <th className="py-3 px-4 text-center">状態</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {urgentBatches.length === 0 ? (
                <tr>
                  <td colSpan={8} className="text-center py-8 text-slate-400">
                    現在、緊急対応が必要なロットはありません
                  </td>
                </tr>
              ) : (
                urgentBatches.map((b) => (
                  <tr key={b.id} className="hover:bg-slate-50 transition">
                    <td className="py-3 px-4 font-mono font-bold text-indigo-600">{b.batchCode}</td>
                    <td className="py-3 px-4 font-bold text-slate-900">{b.productName}</td>
                    <td className="py-3 px-4 text-slate-500">{b.categoryName.split(' ')[0]}</td>
                    <td className="py-3 px-4 text-right font-bold">¥{b.price.toLocaleString()}</td>
                    <td className="py-3 px-4 text-right font-black text-rose-600">{b.remainingQuantity}個</td>
                    <td className="py-3 px-4 text-slate-600">
                      {new Date(b.expiryDate).toLocaleString('ja-JP')}
                    </td>
                    <td className="py-3 px-4 font-bold text-amber-700">残り {b.hoursUntilExpiry} 時間</td>
                    <td className="py-3 px-4 text-center">
                      <span
                        className={`inline-block px-2.5 py-1 rounded-full text-[11px] font-bold ${
                          b.status === 'CRITICAL'
                            ? 'bg-rose-100 text-rose-800'
                            : 'bg-amber-100 text-amber-800'
                        }`}
                      >
                        {b.statusJapanese}
                      </span>
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      </div>

      {/* AI Multi-Agent Workflow Visualizer Modal */}
      <AiAgentWorkflowModal
        isOpen={isAiModalOpen}
        onClose={() => setIsAiModalOpen(false)}
        onComplete={loadData}
      />
    </div>
  );
}

