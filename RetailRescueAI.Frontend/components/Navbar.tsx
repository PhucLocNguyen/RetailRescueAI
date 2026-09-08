'use client';

import Link from 'next/link';
import { usePathname } from 'next/navigation';
import { ShoppingBag, LayoutDashboard, Sparkles, Store, ShieldCheck, UserCheck } from 'lucide-react';

export default function Navbar() {
  const pathname = usePathname();
  const isPos = pathname.startsWith('/pos');
  const isManagement = pathname.startsWith('/management');

  return (
    <header className="bg-slate-900 text-white border-b border-slate-800 sticky top-0 z-50 shadow-md">
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 h-16 flex items-center justify-between">
        <div className="flex items-center space-x-6">
          <Link href="/" className="flex items-center space-x-2.5">
            <div className="bg-emerald-500 text-slate-950 p-1.5 rounded-lg font-black text-xl flex items-center justify-center shadow">
              <Sparkles className="w-5 h-5 text-slate-900" />
            </div>
            <div>
              <span className="font-extrabold text-lg tracking-tight text-white flex items-center gap-1.5">
                RetailRescue <span className="text-emerald-400">AI</span>
              </span>
              <span className="block text-[10px] text-slate-400 font-medium -mt-1">
                食品廃棄防止＆販促最適化エージェント
              </span>
            </div>
          </Link>

          <div className="hidden md:flex items-center gap-2 pl-4 border-l border-slate-800 text-xs text-slate-300">
            <Store className="w-3.5 h-3.5 text-emerald-400" />
            <span>ライフマート 新宿東口店 #01</span>
          </div>
        </div>

        {/* Role Navigation Switcher */}
        <div className="flex items-center space-x-3">
          <Link
            href="/pos"
            className={`flex items-center space-x-2 px-3.5 py-1.5 rounded-lg text-sm font-semibold transition-all shadow-sm ${
              isPos
                ? 'bg-emerald-500 text-slate-950 font-bold ring-2 ring-emerald-400'
                : 'bg-slate-800 text-slate-200 hover:bg-slate-700'
            }`}
          >
            <ShoppingBag className="w-4 h-4" />
            <span>スタッフ POS レジ</span>
          </Link>

          <Link
            href="/management"
            className={`flex items-center space-x-2 px-3.5 py-1.5 rounded-lg text-sm font-semibold transition-all shadow-sm ${
              isManagement
                ? 'bg-indigo-600 text-white font-bold ring-2 ring-indigo-400'
                : 'bg-slate-800 text-slate-200 hover:bg-slate-700'
            }`}
          >
            <LayoutDashboard className="w-4 h-4" />
            <span>店長ポータル</span>
          </Link>

          <div className="hidden lg:flex items-center gap-1.5 pl-3 border-l border-slate-800 text-xs text-slate-400">
            <span className="inline-block w-2 h-2 rounded-full bg-emerald-400 animate-pulse"></span>
            <span>AI稼働中</span>
          </div>
        </div>
      </div>
    </header>
  );
}

