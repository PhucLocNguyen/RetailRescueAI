'use client';

import { useState, useEffect } from 'react';
import { Promotion, fetchPromotions, approvePromotion, rejectPromotion } from '@/lib/api';
import { Tag, CheckCircle, XCircle, Clock, Plus, RefreshCw } from 'lucide-react';

export default function PromotionsManagementPage() {
  const [promotions, setPromotions] = useState<Promotion[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [statusFilter, setStatusFilter] = useState('APPROVED');

  useEffect(() => {
    loadPromotions();
  }, [statusFilter]);

  async function loadPromotions() {
    setIsLoading(true);
    try {
      const data = await fetchPromotions(statusFilter === 'ALL' ? undefined : statusFilter);
      setPromotions(data);
    } catch (err) {
      console.error(err);
    } finally {
      setIsLoading(false);
    }
  }

  async function handleApprove(id: number, name: string) {
    if (!confirm(`プロモーション「${name}」を承認しますか？`)) return;
    try {
      await approvePromotion(id);
      alert('プロモーションを承認しました！');
      loadPromotions();
    } catch (err) {
      alert('承認に失敗しました。');
    }
  }

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="bg-white rounded-2xl p-6 border border-slate-200 shadow-sm flex flex-col md:flex-row md:items-center md:justify-between gap-4">
        <div>
          <span className="text-xs font-bold text-indigo-600 uppercase tracking-wider bg-indigo-50 px-2.5 py-1 rounded-md">
            プロモーション統括
          </span>
          <h1 className="text-2xl font-black text-slate-900 mt-1">プロモーション管理 ＆ 稼働状況</h1>
          <p className="text-xs text-slate-500 mt-0.5">
            承認済みのプロモーションのみがPOSレジでお客様へのおすすめとして反映されます
          </p>
        </div>

        <button
          onClick={loadPromotions}
          className="px-4 py-2 bg-white border border-slate-200 hover:bg-slate-50 text-slate-700 font-bold text-xs rounded-xl shadow-xs flex items-center gap-2"
        >
          <RefreshCw className={`w-3.5 h-3.5 ${isLoading ? 'animate-spin' : ''}`} />
          <span>更新</span>
        </button>
      </div>

      {/* Filter Tabs */}
      <div className="flex gap-2 text-xs">
        {[
          { id: 'APPROVED', label: '有効・承認済 (APPROVED)' },
          { id: 'PENDING', label: '承認待ち (PENDING)' },
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

      {/* Promotions Table */}
      <div className="bg-white rounded-2xl border border-slate-200 shadow-sm overflow-hidden">
        <div className="overflow-x-auto">
          <table className="w-full text-left text-xs">
            <thead className="bg-slate-50 text-slate-600 font-bold border-b border-slate-200">
              <tr>
                <th className="py-3.5 px-4">コード</th>
                <th className="py-3.5 px-4">施策名</th>
                <th className="py-3.5 px-4">タイプ</th>
                <th className="py-3.5 px-4">対象商品</th>
                <th className="py-3.5 px-4 text-right">割引率</th>
                <th className="py-3.5 px-4">実施期間</th>
                <th className="py-3.5 px-4">作成元</th>
                <th className="py-3.5 px-4">承認者</th>
                <th className="py-3.5 px-4 text-center">状態</th>
                {statusFilter === 'PENDING' && <th className="py-3.5 px-4 text-center">操作</th>}
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {isLoading ? (
                <tr>
                  <td colSpan={10} className="text-center py-10 text-slate-400">
                    読み込み中...
                  </td>
                </tr>
              ) : promotions.length === 0 ? (
                <tr>
                  <td colSpan={10} className="text-center py-10 text-slate-400">
                    該当するプロモーションがありません
                  </td>
                </tr>
              ) : (
                promotions.map((p) => (
                  <tr key={p.id} className="hover:bg-slate-50 transition">
                    <td className="py-3.5 px-4 font-mono font-bold text-indigo-600">{p.promotionCode}</td>
                    <td className="py-3.5 px-4 font-bold text-slate-900">{p.name}</td>
                    <td className="py-3.5 px-4 text-slate-500">{p.promotionType}</td>
                    <td className="py-3.5 px-4 text-slate-800 font-medium">
                      {p.targetProductName || '全体'}
                    </td>
                    <td className="py-3.5 px-4 text-right font-black text-emerald-600">
                      {p.discountPercent ? `${p.discountPercent}% OFF` : '-'}
                    </td>
                    <td className="py-3.5 px-4 text-slate-600">
                      {new Date(p.startTime).toLocaleTimeString('ja-JP', { hour: '2-digit', minute: '2-digit' })} 〜{' '}
                      {new Date(p.endTime).toLocaleTimeString('ja-JP', { hour: '2-digit', minute: '2-digit' })}
                    </td>
                    <td className="py-3.5 px-4 text-slate-500 font-medium">
                      <span className="bg-slate-100 px-2 py-0.5 rounded text-[10px]">
                        {p.createdVia}
                      </span>
                    </td>
                    <td className="py-3.5 px-4 text-slate-600">{p.approvedBy || '-'}</td>
                    <td className="py-3.5 px-4 text-center">
                      <span
                        className={`inline-block px-2.5 py-1 rounded-full text-[10px] font-bold ${
                          p.status === 'APPROVED'
                            ? 'bg-emerald-100 text-emerald-800'
                            : p.status === 'PENDING'
                            ? 'bg-amber-100 text-amber-800'
                            : 'bg-slate-100 text-slate-600'
                        }`}
                      >
                        {p.status}
                      </span>
                    </td>
                    {statusFilter === 'PENDING' && (
                      <td className="py-3.5 px-4 text-center">
                        <button
                          onClick={() => handleApprove(p.id, p.name)}
                          className="px-3 py-1 bg-emerald-600 hover:bg-emerald-500 text-white font-bold rounded-lg text-[10px]"
                        >
                          承認
                        </button>
                      </td>
                    )}
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

