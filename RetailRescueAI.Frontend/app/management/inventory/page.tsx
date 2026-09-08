'use client';

import { useState, useEffect } from 'react';
import { InventoryBatch, fetchBatches } from '@/lib/api';
import { Boxes, Search, Filter, RefreshCw, AlertCircle, CheckCircle } from 'lucide-react';

export default function InventoryManagementPage() {
  const [batches, setBatches] = useState<InventoryBatch[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [statusFilter, setStatusFilter] = useState('ALL');
  const [search, setSearch] = useState('');

  useEffect(() => {
    loadBatches();
  }, []);

  async function loadBatches() {
    setIsLoading(true);
    try {
      const data = await fetchBatches();
      setBatches(data);
    } catch (err) {
      console.error(err);
    } finally {
      setIsLoading(false);
    }
  }

  const filtered = batches.filter((b) => {
    const matchStatus = statusFilter === 'ALL' || b.status === statusFilter;
    const matchSearch =
      !search ||
      b.productName.toLowerCase().includes(search.toLowerCase()) ||
      b.batchCode.toLowerCase().includes(search.toLowerCase()) ||
      b.productCode.toLowerCase().includes(search.toLowerCase());
    return matchStatus && matchSearch;
  });

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="bg-white rounded-2xl p-6 border border-slate-200 shadow-sm flex flex-col md:flex-row md:items-center md:justify-between gap-4">
        <div>
          <span className="text-xs font-bold text-indigo-600 uppercase tracking-wider bg-indigo-50 px-2.5 py-1 rounded-md">
            在庫管理（ロット単位）
          </span>
          <h1 className="text-2xl font-black text-slate-900 mt-1">ロット別 在庫 ＆ 賞味期限追跡</h1>
          <p className="text-xs text-slate-500 mt-0.5">
            同一商品でも製造日・賞味期限ごとに独立して在庫と廃棄リスクを個別監視します
          </p>
        </div>

        <button
          onClick={loadBatches}
          className="px-4 py-2 bg-white border border-slate-200 hover:bg-slate-50 text-slate-700 font-bold text-xs rounded-xl shadow-xs flex items-center gap-2"
        >
          <RefreshCw className={`w-3.5 h-3.5 ${isLoading ? 'animate-spin' : ''}`} />
          <span>データを更新</span>
        </button>
      </div>

      {/* Filter & Search Toolbar */}
      <div className="bg-white p-4 rounded-2xl border border-slate-200 shadow-xs flex flex-col md:flex-row gap-3 items-center justify-between">
        <div className="relative w-full md:w-80">
          <Search className="w-4 h-4 absolute left-3 top-3 text-slate-400" />
          <input
            type="text"
            placeholder="ロット番号、商品名で検索..."
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            className="w-full pl-9 pr-4 py-2 border border-slate-200 rounded-xl text-xs focus:outline-none focus:ring-2 focus:ring-indigo-500"
          />
        </div>

        {/* Status Filter Pills */}
        <div className="flex flex-wrap gap-1.5 text-xs w-full md:w-auto">
          {[
            { id: 'ALL', label: 'すべて' },
            { id: 'CRITICAL', label: '🔴 危機的' },
            { id: 'AT_RISK', label: '🟠 期限間近' },
            { id: 'AVAILABLE', label: '🟢 正常' },
            { id: 'SOLD_OUT', label: '⚪ 完売' },
          ].map((st) => (
            <button
              key={st.id}
              onClick={() => setStatusFilter(st.id)}
              className={`px-3 py-1.5 rounded-lg font-bold transition ${
                statusFilter === st.id
                  ? 'bg-indigo-600 text-white shadow'
                  : 'bg-slate-100 hover:bg-slate-200 text-slate-700'
              }`}
            >
              {st.label}
            </button>
          ))}
        </div>
      </div>

      {/* Batches Table */}
      <div className="bg-white rounded-2xl border border-slate-200 shadow-sm overflow-hidden">
        <div className="overflow-x-auto">
          <table className="w-full text-left text-xs">
            <thead className="bg-slate-50 text-slate-600 font-bold border-b border-slate-200">
              <tr>
                <th className="py-3.5 px-4">ロット番号</th>
                <th className="py-3.5 px-4">商品名</th>
                <th className="py-3.5 px-4">カテゴリ</th>
                <th className="py-3.5 px-4 text-right">定価</th>
                <th className="py-3.5 px-4 text-right">初期数</th>
                <th className="py-3.5 px-4 text-right">現在庫数</th>
                <th className="py-3.5 px-4">製造日</th>
                <th className="py-3.5 px-4">賞味期限</th>
                <th className="py-3.5 px-4">期限まで</th>
                <th className="py-3.5 px-4 text-center">状態</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {isLoading ? (
                <tr>
                  <td colSpan={10} className="text-center py-10 text-slate-400">
                    読み込み中...
                  </td>
                </tr>
              ) : filtered.length === 0 ? (
                <tr>
                  <td colSpan={10} className="text-center py-10 text-slate-400">
                    該当するロットがありません
                  </td>
                </tr>
              ) : (
                filtered.map((b) => (
                  <tr key={b.id} className="hover:bg-slate-50 transition">
                    <td className="py-3.5 px-4 font-mono font-bold text-indigo-600">{b.batchCode}</td>
                    <td className="py-3.5 px-4 font-bold text-slate-900">{b.productName}</td>
                    <td className="py-3.5 px-4 text-slate-500">{b.categoryName.split(' ')[0]}</td>
                    <td className="py-3.5 px-4 text-right font-bold">¥{b.price.toLocaleString()}</td>
                    <td className="py-3.5 px-4 text-right text-slate-500">{b.initialQuantity}個</td>
                    <td className="py-3.5 px-4 text-right font-black text-slate-900">
                      <span className={b.remainingQuantity <= 15 ? 'text-rose-600' : ''}>
                        {b.remainingQuantity}個
                      </span>
                    </td>
                    <td className="py-3.5 px-4 text-slate-500">
                      {new Date(b.productionDate).toLocaleString('ja-JP')}
                    </td>
                    <td className="py-3.5 px-4 text-slate-700 font-medium">
                      {new Date(b.expiryDate).toLocaleString('ja-JP')}
                    </td>
                    <td className="py-3.5 px-4 font-bold">
                      {b.hoursUntilExpiry <= 0 ? (
                        <span className="text-slate-400">期限切れ</span>
                      ) : b.hoursUntilExpiry <= 12 ? (
                        <span className="text-rose-600">残り {b.hoursUntilExpiry} 時間</span>
                      ) : b.hoursUntilExpiry <= 24 ? (
                        <span className="text-amber-600">残り {b.hoursUntilExpiry} 時間</span>
                      ) : (
                        <span className="text-slate-600">残り {Math.round(b.hoursUntilExpiry / 24)} 日</span>
                      )}
                    </td>
                    <td className="py-3.5 px-4 text-center">
                      <span
                        className={`inline-block px-2.5 py-1 rounded-full text-[10px] font-bold ${
                          b.status === 'CRITICAL'
                            ? 'bg-rose-100 text-rose-800'
                            : b.status === 'AT_RISK'
                            ? 'bg-amber-100 text-amber-800'
                            : b.status === 'AVAILABLE'
                            ? 'bg-emerald-100 text-emerald-800'
                            : 'bg-slate-100 text-slate-600'
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
    </div>
  );
}

