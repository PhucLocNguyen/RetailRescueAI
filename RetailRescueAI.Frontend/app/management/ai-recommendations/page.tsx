'use client';

import { useState, useEffect } from 'react';
import {
  AIRecommendation,
  fetchAIRecommendations,
  approveAIRecommendation,
  rejectAIRecommendation,
} from '@/lib/api';
import AiAgentWorkflowModal from '@/components/AiAgentWorkflowModal';
import {
  Sparkles,
  CheckCircle2,
  XCircle,
  Clock,
  Coins,
  ShieldCheck,
  TrendingDown,
  RefreshCw,
  FileText,
  AlertTriangle,
  Play,
  Sliders,
} from 'lucide-react';

export default function AiRecommendationsPage() {
  const [recommendations, setRecommendations] = useState<AIRecommendation[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [statusFilter, setStatusFilter] = useState('PENDING');
  const [processingId, setProcessingId] = useState<number | null>(null);
  const [customDiscounts, setCustomDiscounts] = useState<Record<number, number>>({});
  const [isAiModalOpen, setIsAiModalOpen] = useState(false);

  useEffect(() => {
    loadRecommendations();
  }, []);

  async function loadRecommendations() {
    setIsLoading(true);
    try {
      const data = await fetchAIRecommendations();
      setRecommendations(data);
    } catch (err) {
      console.error(err);
    } finally {
      setIsLoading(false);
    }
  }

  async function handleApprove(id: number, title: string, discountPercent: number) {
    if (!confirm(`提案「${title}」を ${discountPercent}% 割引で承認しますか？\n承認すると直ちにPOSレジで適用されます。`)) {
      return;
    }

    setProcessingId(id);
    try {
      await approveAIRecommendation(id, discountPercent);
      alert(`プロモーションを ${discountPercent}% 割引で承認しました！レジ画面で即時有効になります。`);
      await loadRecommendations();
    } catch (err) {
      alert('承認処理に失敗しました。');
      console.error(err);
    } finally {
      setProcessingId(null);
    }
  }

  async function handleReject(id: number, title: string) {
    const reason = prompt(`提案「${title}」の却下理由を入力してください:`, '店舗方針による見合わせ');
    if (reason === null) return;

    setProcessingId(id);
    try {
      await rejectAIRecommendation(id, reason);
      alert('提案を却下しました。');
      await loadRecommendations();
    } catch (err) {
      alert('却下処理に失敗しました。');
      console.error(err);
    } finally {
      setProcessingId(null);
    }
  }

  const filtered = recommendations.filter((r) => {
    if (statusFilter === 'ALL') return true;
    return r.status === statusFilter;
  });

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="bg-white rounded-2xl p-6 border border-slate-200 shadow-sm flex flex-col md:flex-row md:items-center md:justify-between gap-4">
        <div>
          <div className="flex items-center gap-2">
            <span className="text-xs font-bold text-indigo-600 uppercase tracking-wider bg-indigo-50 px-2.5 py-1 rounded-md">
              Human-in-the-Loop 審査センター
            </span>
          </div>
          <h1 className="text-2xl font-black text-slate-900 mt-1">AIプロモーション提案の審査・承認</h1>
          <p className="text-xs text-slate-500 mt-0.5">
            AIエージェントが算出した提案と根拠を店長が精査し、承認されたものだけがレジで有効化されます
          </p>
        </div>

        <div className="flex items-center gap-2.5">
          <button
            onClick={() => setIsAiModalOpen(true)}
            className="px-4 py-2 bg-gradient-to-r from-purple-600 via-indigo-600 to-violet-600 hover:from-purple-500 hover:to-violet-500 text-white font-bold text-xs rounded-xl shadow-xs flex items-center gap-1.5 transition active:scale-95"
          >
            <Sparkles className="w-3.5 h-3.5 text-amber-300" />
            <span>AIパイプライン実演デモ</span>
          </button>

          <button
            onClick={loadRecommendations}
            className="px-4 py-2 bg-white border border-slate-200 hover:bg-slate-50 text-slate-700 font-bold text-xs rounded-xl shadow-xs flex items-center gap-2"
          >
            <RefreshCw className={`w-3.5 h-3.5 ${isLoading ? 'animate-spin' : ''}`} />
            <span>一覧を更新</span>
          </button>
        </div>
      </div>

      {/* Filter Tabs */}
      <div className="flex gap-2 text-xs">
        {[
          { id: 'PENDING', label: '承認待ち (PENDING)' },
          { id: 'APPROVED', label: '承認済 (APPROVED)' },
          { id: 'REJECTED', label: '却下済 (REJECTED)' },
          { id: 'ALL', label: 'すべて' },
        ].map((tab) => (
          <button
            key={tab.id}
            onClick={() => setStatusFilter(tab.id)}
            className={`px-4 py-2 rounded-xl font-bold transition ${
              statusFilter === tab.id
                ? 'bg-slate-900 text-white shadow'
                : 'bg-white text-slate-600 border border-slate-200 hover:bg-slate-100'
            }`}
          >
            {tab.label}
          </button>
        ))}
      </div>

      {/* Recommendations Cards List */}
      <div className="space-y-4">
        {isLoading ? (
          <div className="text-center py-20 text-slate-400 text-sm">提案データを読み込み中...</div>
        ) : filtered.length === 0 ? (
          <div className="bg-white rounded-2xl p-12 text-center text-slate-400 border border-slate-200">
            {statusFilter === 'PENDING'
              ? '現在、審査待ちのAI提案はありません。'
              : '該当する提案はありません。'}
          </div>
        ) : (
          filtered.map((rec) => {
            const isPending = rec.status === 'PENDING';
            const isApproved = rec.status === 'APPROVED';

            return (
              <div
                key={rec.id}
                className={`bg-white rounded-2xl border p-6 shadow-sm transition flex flex-col gap-5 ${
                  isPending
                    ? 'border-indigo-300 ring-1 ring-indigo-200'
                    : isApproved
                    ? 'border-emerald-200 bg-emerald-50/10'
                    : 'border-slate-200 opacity-75'
                }`}
              >
                {/* Proposal Top Header */}
                <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 pb-4 border-b border-slate-100">
                  <div className="space-y-1">
                    <div className="flex items-center gap-2">
                      <span className="font-mono text-xs text-slate-400 font-bold">{rec.recommendationCode}</span>
                      <span
                        className={`text-[10px] font-bold px-2 py-0.5 rounded-full ${
                          rec.riskLevel === 'CRITICAL'
                            ? 'bg-rose-100 text-rose-800'
                            : 'bg-amber-100 text-amber-800'
                        }`}
                      >
                        危険度: {rec.riskLevel}
                      </span>
                      <span
                        className={`text-[10px] font-bold px-2 py-0.5 rounded-full ${
                          isPending
                            ? 'bg-indigo-100 text-indigo-800'
                            : isApproved
                            ? 'bg-emerald-100 text-emerald-800'
                            : 'bg-slate-100 text-slate-700'
                        }`}
                      >
                        状態: {rec.status}
                      </span>
                    </div>

                    <h3 className="text-xl font-black text-slate-900 flex items-center gap-2">
                      <span>{rec.recommendedAction}</span>
                    </h3>
                    <p className="text-xs text-slate-500">
                      対象商品: <strong className="text-slate-800">{rec.targetProductName}</strong> / ロット:{' '}
                      <strong className="text-indigo-600 font-mono">{rec.targetBatchCode}</strong>
                    </p>
                  </div>

                  {/* Impact Highlights */}
                  <div className="flex gap-4 sm:border-l sm:pl-5 border-slate-100 text-xs">
                    <div>
                      <span className="text-slate-400 block text-[10px]">期待販売数</span>
                      <span className="font-black text-slate-900 text-base">{rec.expectedSales} 個</span>
                    </div>
                    <div>
                      <span className="text-slate-400 block text-[10px]">廃棄救済見込</span>
                      <span className="font-black text-emerald-600 text-base">+{rec.expectedWasteReduction} 個</span>
                    </div>
                    <div>
                      <span className="text-slate-400 block text-[10px]">期待回収収益</span>
                      <span className="font-black text-slate-900 text-base">¥{rec.expectedRevenue.toLocaleString()}</span>
                    </div>
                  </div>
                </div>

                {/* AI Reasoning Text & Evidence */}
                <div className="grid md:grid-cols-3 gap-5">
                  <div className="md:col-span-2 bg-slate-50 p-4 rounded-xl border border-slate-200">
                    <h4 className="text-xs font-bold text-slate-700 flex items-center gap-1.5 mb-2">
                      <Sparkles className="w-3.5 h-3.5 text-indigo-600" />
                      <span>AIエージェントの算出根拠・理由（日本語解説）</span>
                    </h4>
                    <p className="text-xs text-slate-700 leading-relaxed whitespace-pre-line font-sans">
                      {rec.reason}
                    </p>
                  </div>

                  {/* Evidence Table */}
                  <div className="bg-slate-50 p-4 rounded-xl border border-slate-200 space-y-2">
                    <h4 className="text-xs font-bold text-slate-700 flex items-center gap-1.5 mb-2">
                      <FileText className="w-3.5 h-3.5 text-indigo-600" />
                      <span>エビデンス数値データ</span>
                    </h4>
                    <div className="space-y-1.5 text-xs">
                      {rec.evidences.map((ev, idx) => (
                        <div key={idx} className="flex justify-between border-b border-slate-200/60 pb-1">
                          <span className="text-slate-500 text-[11px]">{ev.key}</span>
                          <span className="font-mono font-bold text-slate-800">{ev.value}</span>
                        </div>
                      ))}
                    </div>
                  </div>
                </div>

                {/* Manager Discount % Adjuster (Only on PENDING proposals) */}
                {isPending && (
                  <div className="bg-indigo-50/60 p-4 rounded-xl border border-indigo-200/80 flex flex-col md:flex-row md:items-center justify-between gap-3 text-xs">
                    <div className="space-y-1">
                      <div className="flex items-center gap-1.5 font-black text-indigo-950">
                        <Sliders className="w-4 h-4 text-indigo-600" />
                        <span>店長による割引率の調整（AI推奨: {rec.recommendedDiscountPercent || 20}% OFF）</span>
                      </div>
                      <p className="text-[11px] text-slate-500">
                        店舗の立地・天候・客足に合わせて割引率を微調整できます。承認すると即時POSレジへ反映されます。
                      </p>
                    </div>

                    <div className="flex items-center gap-3 bg-white p-2 rounded-xl border border-indigo-200 shadow-xs">
                      {/* Quick percent buttons */}
                      <div className="flex items-center gap-1">
                        {[10, 15, 20, 25, 30, 40, 50].map((pct) => {
                          const currentPct = customDiscounts[rec.id] ?? rec.recommendedDiscountPercent ?? 20;
                          const isSelected = currentPct === pct;
                          return (
                            <button
                              key={pct}
                              onClick={() => setCustomDiscounts((prev) => ({ ...prev, [rec.id]: pct }))}
                              className={`px-2.5 py-1 rounded-lg font-black text-xs transition ${
                                isSelected
                                  ? 'bg-indigo-600 text-white shadow-xs'
                                  : 'bg-slate-50 border border-slate-200 text-slate-700 hover:bg-indigo-50'
                              }`}
                            >
                              {pct}%
                            </button>
                          );
                        })}
                      </div>

                      {/* Manual input */}
                      <div className="flex items-center gap-1 pl-2 border-l border-slate-200">
                        <input
                          type="number"
                          min="5"
                          max="80"
                          value={customDiscounts[rec.id] ?? rec.recommendedDiscountPercent ?? 20}
                          onChange={(e) => {
                            const val = Math.min(80, Math.max(5, Number(e.target.value)));
                            setCustomDiscounts((prev) => ({ ...prev, [rec.id]: val }));
                          }}
                          className="w-12 px-1.5 py-0.5 bg-slate-50 border border-slate-300 rounded text-center font-black text-slate-900 focus:outline-none focus:ring-1 focus:ring-indigo-500"
                        />
                        <span className="font-bold text-slate-600">% OFF</span>
                      </div>
                    </div>
                  </div>
                )}

                {/* Guardrails Validation & Approval Actions */}
                <div className="pt-2 flex flex-col sm:flex-row sm:items-center justify-between gap-4 border-t border-slate-100 text-xs">
                  <div className="flex items-center gap-2 text-slate-500">
                    <ShieldCheck className="w-4 h-4 text-emerald-600" />
                    <span>Reviser Agent検証済: BR-003（賞味期限内）, BR-006（有効在庫）, 粗利基準クリア</span>
                  </div>

                  {isPending && (
                    <div className="flex gap-2">
                      <button
                        disabled={processingId === rec.id}
                        onClick={() => handleReject(rec.id, rec.recommendedAction)}
                        className="px-4 py-2 border border-slate-300 hover:bg-slate-100 text-slate-700 font-bold rounded-xl transition flex items-center gap-1.5"
                      >
                        <XCircle className="w-4 h-4 text-slate-400" />
                        <span>却下</span>
                      </button>
                      <button
                        disabled={processingId === rec.id}
                        onClick={() => {
                          const activeDiscount = customDiscounts[rec.id] ?? rec.recommendedDiscountPercent ?? 20;
                          handleApprove(rec.id, rec.recommendedAction, activeDiscount);
                        }}
                        className="px-6 py-2 bg-emerald-600 hover:bg-emerald-500 text-white font-black rounded-xl shadow-md transition flex items-center gap-1.5 active:scale-95"
                      >
                        <CheckCircle2 className="w-4 h-4 text-white" />
                        <span>
                          {customDiscounts[rec.id] ?? rec.recommendedDiscountPercent ?? 20}% OFF で承認して有効化
                        </span>
                      </button>
                    </div>
                  )}
                </div>
              </div>
            );
          })
        )}
      </div>

      {/* AI Multi-Agent Workflow Visualizer Modal */}
      <AiAgentWorkflowModal
        isOpen={isAiModalOpen}
        onClose={() => setIsAiModalOpen(false)}
        onComplete={loadRecommendations}
      />
    </div>
  );
}

