'use client';

import { useState, useEffect } from 'react';
import Link from 'next/link';
import { ExpiryRiskItem, fetchExpiryRisks } from '@/lib/api';
import { AlertOctagon, TrendingDown, ArrowRight, Sparkles, RefreshCw, Info } from 'lucide-react';

export default function ExpiryRiskPage() {
  const [risks, setRisks] = useState<ExpiryRiskItem[]>([]);
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    loadRisks();
  }, []);

  async function loadRisks() {
    setIsLoading(true);
    try {
      const data = await fetchExpiryRisks();
      setRisks(data);
    } catch (err) {
      console.error(err);
    } finally {
      setIsLoading(false);
    }
  }

  const criticalAndAtRisk = risks.filter((r) => r.riskLevel === 'CRITICAL' || r.riskLevel === 'AT_RISK');

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="bg-white rounded-2xl p-6 border border-slate-200 shadow-sm flex flex-col md:flex-row md:items-center md:justify-between gap-4">
        <div>
          <span className="text-xs font-bold text-rose-600 uppercase tracking-wider bg-rose-50 px-2.5 py-1 rounded-md">
            リスク分析エンジン (Sales Agent & Expiry Agent)
          </span>
          <h1 className="text-2xl font-black text-slate-900 mt-1">賞味期限切れ廃棄リスク分析</h1>
          <p className="text-xs text-slate-500 mt-0.5">
            過去7日間の日販速度と残存時間から「通常販売で売り切れない在庫」を自動特定します
          </p>
        </div>

        <div className="flex gap-2">
          <button
            onClick={loadRisks}
            className="px-4 py-2 bg-white border border-slate-200 hover:bg-slate-50 text-slate-700 font-bold text-xs rounded-xl shadow-xs flex items-center gap-2"
          >
            <RefreshCw className={`w-3.5 h-3.5 ${isLoading ? 'animate-spin' : ''}`} />
            <span>再分析</span>
          </button>
          <Link
            href="/management/ai-recommendations"
            className="px-4 py-2 bg-indigo-600 hover:bg-indigo-500 text-white font-bold text-xs rounded-xl shadow-xs flex items-center gap-2"
          >
            <Sparkles className="w-3.5 h-3.5 text-amber-300" />
            <span>AI提案へ進む</span>
          </Link>
        </div>
      </div>

      {/* Explanation Box */}
      <div className="bg-indigo-50 border border-indigo-200 rounded-2xl p-5 text-indigo-950 text-xs flex gap-3.5 items-start">
        <Info className="w-5 h-5 text-indigo-600 shrink-0 mt-0.5" />
        <div className="space-y-1">
          <p className="font-bold text-sm text-indigo-900">AIエージェントの廃棄リスク判定式</p>
          <p className="leading-relaxed text-indigo-800">
            <strong>潜在的廃棄数 ＝ 現在庫数 −（平均日販速度 × 残存日数）</strong><br />
            残在庫数が通常販売ペースを大幅に上回り、かつ賞味期限まで24時間未満のロットを「要対応（AT_RISK / CRITICAL）」に分類し、プロモーションを推奨します。
          </p>
        </div>
      </div>

      {/* Risk Analysis Table */}
      <div className="bg-white rounded-2xl border border-slate-200 shadow-sm overflow-hidden">
        <div className="p-5 border-b border-slate-100">
          <h2 className="font-bold text-base text-slate-900">ロット別 廃棄リスク一覧</h2>
          <p className="text-xs text-slate-500 mt-0.5">残存時間・通常予測販売数・潜在廃棄ロスの詳細</p>
        </div>

        <div className="overflow-x-auto">
          <table className="w-full text-left text-xs">
            <thead className="bg-slate-50 text-slate-600 font-bold border-b border-slate-200">
              <tr>
                <th className="py-3.5 px-4">ロット</th>
                <th className="py-3.5 px-4">商品名</th>
                <th className="py-3.5 px-4 text-right">現在庫</th>
                <th className="py-3.5 px-4">賞味期限まで</th>
                <th className="py-3.5 px-4 text-right">平均日販速度</th>
                <th className="py-3.5 px-4 text-right">通常予測販売</th>
                <th className="py-3.5 px-4 text-right">潜在廃棄ロス数</th>
                <th className="py-3.5 px-4 text-right">潜在損失金額</th>
                <th className="py-3.5 px-4 text-center">判定リスク</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {isLoading ? (
                <tr>
                  <td colSpan={9} className="text-center py-10 text-slate-400">
                    分析中...
                  </td>
                </tr>
              ) : risks.length === 0 ? (
                <tr>
                  <td colSpan={9} className="text-center py-10 text-slate-400">
                    現在庫データがありません
                  </td>
                </tr>
              ) : (
                risks.map((r) => (
                  <tr key={r.batchId} className="hover:bg-slate-50 transition">
                    <td className="py-3.5 px-4 font-mono font-bold text-indigo-600">{r.batchCode}</td>
                    <td className="py-3.5 px-4 font-bold text-slate-900">{r.productName}</td>
                    <td className="py-3.5 px-4 text-right font-bold text-slate-800">{r.remainingQuantity}個</td>
                    <td className="py-3.5 px-4 font-bold text-amber-700">残り {r.hoursUntilExpiry} 時間</td>
                    <td className="py-3.5 px-4 text-right text-slate-600">{r.averageDailySales} 個/日</td>
                    <td className="py-3.5 px-4 text-right text-slate-600 font-medium">
                      約 {r.estimatedNormalSalesUntilExpiry} 個
                    </td>
                    <td className="py-3.5 px-4 text-right font-black text-rose-600">
                      {r.potentialWasteUnits > 0 ? `${r.potentialWasteUnits} 個` : '0 個'}
                    </td>
                    <td className="py-3.5 px-4 text-right font-black text-rose-700">
                      ¥{r.potentialWasteCost.toLocaleString()}
                    </td>
                    <td className="py-3.5 px-4 text-center">
                      <span
                        className={`inline-block px-2.5 py-1 rounded-full text-[10px] font-bold ${
                          r.riskLevel === 'CRITICAL'
                            ? 'bg-rose-100 text-rose-800'
                            : r.riskLevel === 'AT_RISK'
                            ? 'bg-amber-100 text-amber-800'
                            : 'bg-emerald-100 text-emerald-800'
                        }`}
                      >
                        {r.riskJapanese}
                      </span>
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

