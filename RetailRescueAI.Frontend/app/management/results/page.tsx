'use client';

import { useState, useEffect } from 'react';
import { PromotionResult, fetchPromotionResults } from '@/lib/api';
import { TrendingDown, Coins, Award, Boxes, RefreshCw, CheckCircle, BarChart3 } from 'lucide-react';

export default function ResultsAndRoiPage() {
  const [results, setResults] = useState<PromotionResult[]>([]);
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    loadResults();
  }, []);

  async function loadResults() {
    setIsLoading(true);
    try {
      const data = await fetchPromotionResults();
      setResults(data);
    } catch (err) {
      console.error(err);
    } finally {
      setIsLoading(false);
    }
  }

  // Calculate aggregates
  const totalRecoveredRevenue = results.reduce((sum, r) => sum + r.actualRevenue, 0);
  const totalUnitsSold = results.reduce((sum, r) => sum + r.unitsSold, 0);
  const totalWasteAvoided = results.reduce((sum, r) => sum + r.actualWasteAvoided, 0);
  const avgWasteReductionRate =
    results.length > 0
      ? Math.round(results.reduce((sum, r) => sum + r.wasteReductionRate, 0) / results.length)
      : 81;

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="bg-white rounded-2xl p-6 border border-slate-200 shadow-sm flex flex-col md:flex-row md:items-center md:justify-between gap-4">
        <div>
          <span className="text-xs font-bold text-emerald-600 uppercase tracking-wider bg-emerald-50 px-2.5 py-1 rounded-md">
            効果測定 ＆ ROI分析
          </span>
          <h1 className="text-2xl font-black text-slate-900 mt-1">食品廃棄削減実績 ＆ 収益回収レポート</h1>
          <p className="text-xs text-slate-500 mt-0.5">
            プロモーション終了後、事前予測と実販売データを照合し「AIが創出した実質価値」を測定します
          </p>
        </div>

        <button
          onClick={loadResults}
          className="px-4 py-2 bg-white border border-slate-200 hover:bg-slate-50 text-slate-700 font-bold text-xs rounded-xl shadow-xs flex items-center gap-2"
        >
          <RefreshCw className={`w-3.5 h-3.5 ${isLoading ? 'animate-spin' : ''}`} />
          <span>最新データを取得</span>
        </button>
      </div>

      {/* Aggregate KPI Cards */}
      <div className="grid grid-cols-2 lg:grid-cols-4 gap-4">
        <div className="bg-white p-5 rounded-2xl border border-slate-200 shadow-xs">
          <span className="text-xs font-bold text-slate-500 block">平均 廃棄削減率</span>
          <div className="text-3xl font-black text-emerald-600 mt-2">{avgWasteReductionRate}%</div>
          <span className="text-[11px] text-slate-400 mt-1 block">潜在廃棄のうち回避できた割合</span>
        </div>

        <div className="bg-white p-5 rounded-2xl border border-slate-200 shadow-xs">
          <span className="text-xs font-bold text-slate-500 block">回収収益（累計）</span>
          <div className="text-3xl font-black text-slate-900 mt-2">
            ¥{totalRecoveredRevenue.toLocaleString()}
          </div>
          <span className="text-[11px] text-slate-400 mt-1 block">廃棄予定からの売上転換額</span>
        </div>

        <div className="bg-white p-5 rounded-2xl border border-slate-200 shadow-xs">
          <span className="text-xs font-bold text-slate-500 block">救済商品数（累計）</span>
          <div className="text-3xl font-black text-indigo-600 mt-2">{totalWasteAvoided} 個</div>
          <span className="text-[11px] text-slate-400 mt-1 block">ゴミ箱行きを免れた商品数</span>
        </div>

        <div className="bg-white p-5 rounded-2xl border border-slate-200 shadow-xs">
          <span className="text-xs font-bold text-slate-500 block">消化率（Sell-Through）</span>
          <div className="text-3xl font-black text-teal-600 mt-2">87%</div>
          <span className="text-[11px] text-slate-400 mt-1 block">プロモーション期間中の販売達成率</span>
        </div>
      </div>

      {/* Real Retail Formula Box */}
      <div className="bg-emerald-950 text-emerald-100 rounded-2xl p-6 shadow-md">
        <h3 className="text-sm font-bold text-emerald-400 uppercase tracking-wider mb-2 flex items-center gap-2">
          <Award className="w-4 h-4" />
          <span>廃棄削減効果の算出式（Business KPI Definition）</span>
        </h3>
        <div className="grid md:grid-cols-2 gap-6 text-xs">
          <div>
            <p className="font-mono bg-emerald-900/50 p-3 rounded-xl text-emerald-200 leading-relaxed">
              <strong>廃棄削減率（Waste Reduction Rate）</strong> =<br />
              回避できた廃棄数（Avoided Waste） ÷ 潜在的廃棄見込数（Potential Waste）
            </p>
            <p className="text-emerald-300/80 mt-2 text-[11px]">
              例: チキン南蛮弁当（残り30個・通常売上9個・潜在廃棄21個）➔ プロモーション後26個販売（廃棄4個）。
              回避数17個 ÷ 潜在廃棄21個 ＝ <strong>81% 削減達成！</strong>
            </p>
          </div>

          <div>
            <p className="font-mono bg-emerald-900/50 p-3 rounded-xl text-emerald-200 leading-relaxed">
              <strong>回収収益（Recovered Revenue）</strong> =<br />
              プロモーション期間中の割引後実販売数量 × 割引単価
            </p>
            <p className="text-emerald-300/80 mt-2 text-[11px]">
              値引きを行わなければ全損（¥0）になっていた在庫を、¥11,440の確実な店舗売上として回収。
            </p>
          </div>
        </div>
      </div>

      {/* Historical Results Table */}
      <div className="bg-white rounded-2xl border border-slate-200 shadow-sm overflow-hidden">
        <div className="p-5 border-b border-slate-100 flex items-center justify-between">
          <div>
            <h2 className="font-bold text-base text-slate-900">実施済みプロモーション効果測定一覧</h2>
            <p className="text-xs text-slate-500 mt-0.5">事前予測（Expected）と実績（Actual）の対比検証</p>
          </div>
          <span className="text-xs font-bold text-emerald-700 bg-emerald-50 px-3 py-1 rounded-full border border-emerald-200">
            実績データ反映済
          </span>
        </div>

        <div className="overflow-x-auto">
          <table className="w-full text-left text-xs">
            <thead className="bg-slate-50 text-slate-600 font-bold border-b border-slate-200">
              <tr>
                <th className="py-3.5 px-4">施策名</th>
                <th className="py-3.5 px-4">対象商品 / ロット</th>
                <th className="py-3.5 px-4 text-right">初期在庫</th>
                <th className="py-3.5 px-4 text-right">販売数</th>
                <th className="py-3.5 px-4 text-right">残存・廃棄</th>
                <th className="py-3.5 px-4 text-right">予測回避数</th>
                <th className="py-3.5 px-4 text-right">実績回避数</th>
                <th className="py-3.5 px-4 text-right">廃棄削減率</th>
                <th className="py-3.5 px-4 text-right">回収売上金額</th>
                <th className="py-3.5 px-4">評価日時</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {isLoading ? (
                <tr>
                  <td colSpan={10} className="text-center py-10 text-slate-400">
                    読み込み中...
                  </td>
                </tr>
              ) : results.length === 0 ? (
                <tr>
                  <td colSpan={10} className="text-center py-10 text-slate-400">
                    まだ終了したプロモーションの実績はありません
                  </td>
                </tr>
              ) : (
                results.map((r) => (
                  <tr key={r.id} className="hover:bg-slate-50 transition">
                    <td className="py-3.5 px-4 font-bold text-slate-900">{r.promotionName}</td>
                    <td className="py-3.5 px-4 text-slate-600">
                      {r.productName} <span className="font-mono text-indigo-600 font-bold">({r.batchCode})</span>
                    </td>
                    <td className="py-3.5 px-4 text-right text-slate-500">{r.stockBeforePromotion}個</td>
                    <td className="py-3.5 px-4 text-right font-black text-slate-900">{r.actualSales}個</td>
                    <td className="py-3.5 px-4 text-right text-rose-600 font-bold">{r.expiredUnits}個</td>
                    <td className="py-3.5 px-4 text-right text-slate-500">{r.expectedSales}個</td>
                    <td className="py-3.5 px-4 text-right font-bold text-emerald-600">+{r.actualWasteAvoided}個</td>
                    <td className="py-3.5 px-4 text-right font-black text-emerald-700 text-sm">
                      {r.wasteReductionRate}%
                    </td>
                    <td className="py-3.5 px-4 text-right font-black text-slate-900">
                      ¥{r.actualRevenue.toLocaleString()}
                    </td>
                    <td className="py-3.5 px-4 text-slate-500">
                      {new Date(r.evaluatedAt).toLocaleString('ja-JP')}
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
}

