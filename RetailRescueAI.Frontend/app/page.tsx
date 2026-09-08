import Link from 'next/link';
import { ShoppingBag, LayoutDashboard, Sparkles, ArrowRight, ShieldCheck, TrendingDown, Clock, CheckCircle2 } from 'lucide-react';
import Navbar from '@/components/Navbar';

export default function Home() {
  return (
    <div className="min-h-screen flex flex-col bg-slate-50">
      <Navbar />

      <main className="flex-1 max-w-6xl mx-auto px-4 py-12 w-full flex flex-col justify-center">
        {/* Header Hero */}
        <div className="text-center max-w-3xl mx-auto mb-12">
          <div className="inline-flex items-center gap-2 px-3.5 py-1.5 rounded-full bg-emerald-100 text-emerald-800 text-xs font-bold mb-4 shadow-sm">
            <Sparkles className="w-4 h-4 text-emerald-600" />
            AIエージェントによる小売食品廃棄防止システム
          </div>
          <h1 className="text-4xl sm:text-5xl font-black text-slate-900 tracking-tight leading-tight">
            小売データを<br className="sm:hidden" />
            <span className="text-emerald-600">スマートな店舗意思決定</span>へ
          </h1>
          <p className="mt-4 text-base sm:text-lg text-slate-600 leading-relaxed">
            「AI提案 ➔ 店長承認 ➔ スタッフ接客 ➔ レジ販売 ➔ 廃棄削減効果測定」<br />
            人間とAIが協調する<strong>Human-in-the-Loop</strong>型の次世代スーパーマーケット支援システム
          </p>
        </div>

        {/* Two Main Entry Roles */}
        <div className="grid md:grid-cols-2 gap-8 max-w-4xl mx-auto w-full mb-14">
          {/* Staff POS Card */}
          <Link
            href="/pos"
            className="group relative bg-white p-8 rounded-2xl border-2 border-slate-200 hover:border-emerald-500 shadow-sm hover:shadow-xl transition-all duration-300 flex flex-col justify-between"
          >
            <div>
              <div className="w-14 h-14 bg-emerald-100 text-emerald-700 rounded-xl flex items-center justify-center mb-6 group-hover:scale-110 transition-transform">
                <ShoppingBag className="w-7 h-7" />
              </div>
              <span className="text-xs font-bold text-emerald-700 uppercase tracking-wider bg-emerald-50 px-2.5 py-1 rounded-md">
                スタッフ専用
              </span>
              <h2 className="text-2xl font-black text-slate-900 mt-2 mb-3 group-hover:text-emerald-600 transition-colors">
                スタッフ POS レジ
              </h2>
              <p className="text-slate-600 text-sm leading-relaxed mb-6">
                バーコード読取、カート商品に応じた承認済プロモーションの即時レコメンド（&lt;500ms）、おすすめ接客、お会計・レシート発行。
              </p>
            </div>
            <div className="flex items-center text-sm font-bold text-emerald-600 group-hover:translate-x-1.5 transition-transform">
              <span>POS画面を開く</span>
              <ArrowRight className="w-4 h-4 ml-2" />
            </div>
          </Link>

          {/* Manager Portal Card */}
          <Link
            href="/management"
            className="group relative bg-white p-8 rounded-2xl border-2 border-slate-200 hover:border-indigo-500 shadow-sm hover:shadow-xl transition-all duration-300 flex flex-col justify-between"
          >
            <div>
              <div className="w-14 h-14 bg-indigo-100 text-indigo-700 rounded-xl flex items-center justify-center mb-6 group-hover:scale-110 transition-transform">
                <LayoutDashboard className="w-7 h-7" />
              </div>
              <span className="text-xs font-bold text-indigo-700 uppercase tracking-wider bg-indigo-50 px-2.5 py-1 rounded-md">
                店長・マネージャー専用
              </span>
              <h2 className="text-2xl font-black text-slate-900 mt-2 mb-3 group-hover:text-indigo-600 transition-colors">
                店長マネジメントポータル
              </h2>
              <p className="text-slate-600 text-sm leading-relaxed mb-6">
                ロット別賞味期限監視、販売速度分析、AI提案の審査・承認、対話型AIアシスタント、廃棄ロス削減率・回収収益の可視化。
              </p>
            </div>
            <div className="flex items-center text-sm font-bold text-indigo-600 group-hover:translate-x-1.5 transition-transform">
              <span>管理ポータルを開く</span>
              <ArrowRight className="w-4 h-4 ml-2" />
            </div>
          </Link>
        </div>

        {/* Demo Guide Pipeline */}
        <div className="bg-white rounded-2xl p-6 sm:p-8 border border-slate-200 shadow-sm max-w-4xl mx-auto w-full">
          <h3 className="text-base font-bold text-slate-800 mb-4 flex items-center gap-2">
            <CheckCircle2 className="w-5 h-5 text-emerald-500" />
            3〜5分 デモシナリオの流れ
          </h3>
          <div className="grid grid-cols-2 sm:grid-cols-4 gap-4 text-xs">
            <div className="bg-slate-50 p-3.5 rounded-xl border border-slate-200">
              <span className="font-black text-indigo-600 block mb-1">STEP 1</span>
              <p className="font-bold text-slate-800">店長ポータルで確認</p>
              <p className="text-slate-500 mt-0.5">チキン南蛮弁当 残り30個（賞味期限18時間）の危険状態を発見</p>
            </div>
            <div className="bg-slate-50 p-3.5 rounded-xl border border-slate-200">
              <span className="font-black text-indigo-600 block mb-1">STEP 2</span>
              <p className="font-bold text-slate-800">AI分析 ＆ 承認</p>
              <p className="text-slate-500 mt-0.5">AIが夕方20%OFFを提案。店長が根拠を確認して「承認」</p>
            </div>
            <div className="bg-slate-50 p-3.5 rounded-xl border border-slate-200">
              <span className="font-black text-emerald-600 block mb-1">STEP 3</span>
              <p className="font-bold text-slate-800">スタッフ POS販売</p>
              <p className="text-slate-500 mt-0.5">レジでチキン弁当をスキャン。承認済プロモーションが即時表示</p>
            </div>
            <div className="bg-slate-50 p-3.5 rounded-xl border border-slate-200">
              <span className="font-black text-emerald-600 block mb-1">STEP 4</span>
              <p className="font-bold text-slate-800">廃棄削減効果の確認</p>
              <p className="text-slate-500 mt-0.5">お会計完了後、ロット在庫が連動減算され削減実績に即時反映</p>
            </div>
          </div>
        </div>
      </main>

      <footer className="border-t border-slate-200 py-6 text-center text-xs text-slate-400 bg-white">
        © 2026 RetailRescue AI. All rights reserved. ライフマート 新宿東口店
      </footer>
    </div>
  );
}

