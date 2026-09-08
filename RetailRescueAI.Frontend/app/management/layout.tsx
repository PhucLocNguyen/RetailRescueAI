'use client';

import Link from 'next/link';
import { usePathname } from 'next/navigation';
import Navbar from '@/components/Navbar';
import {
  LayoutDashboard,
  Boxes,
  AlertOctagon,
  Sparkles,
  Tag,
  MessageSquareText,
  TrendingUp,
} from 'lucide-react';

export default function ManagementLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  const pathname = usePathname();

  const navItems = [
    { href: '/management', label: 'ダッシュボード', icon: LayoutDashboard, exact: true },
    { href: '/management/inventory', label: 'ロット別在庫管理', icon: Boxes },
    { href: '/management/expiry-risk', label: '廃棄リスク分析', icon: AlertOctagon },
    { href: '/management/ai-recommendations', label: 'AIプロモーション提案', icon: Sparkles },
    { href: '/management/promotions', label: 'プロモーション管理', icon: Tag },
    { href: '/management/chatbot', label: 'AIアシスタント', icon: MessageSquareText },
    { href: '/management/results', label: '廃棄削減効果・ROI', icon: TrendingUp },
  ];

  return (
    <div className="min-h-screen flex flex-col bg-slate-100">
      <Navbar />

      {/* Sub-header Navigation Tabs */}
      <div className="bg-white border-b border-slate-200 sticky top-16 z-40 shadow-xs">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
          <nav className="flex space-x-1 overflow-x-auto py-2.5" aria-label="Tabs">
            {navItems.map((item) => {
              const Icon = item.icon;
              const isActive = item.exact
                ? pathname === item.href
                : pathname.startsWith(item.href);

              return (
                <Link
                  key={item.href}
                  href={item.href}
                  className={`flex items-center gap-2 px-3.5 py-2 rounded-xl text-xs sm:text-sm font-bold whitespace-nowrap transition ${
                    isActive
                      ? 'bg-indigo-600 text-white shadow-sm'
                      : 'text-slate-600 hover:text-slate-900 hover:bg-slate-100'
                  }`}
                >
                  <Icon className={`w-4 h-4 ${isActive ? 'text-white' : 'text-slate-400'}`} />
                  <span>{item.label}</span>
                </Link>
              );
            })}
          </nav>
        </div>
      </div>

      <main className="flex-1 max-w-7xl w-full mx-auto p-4 sm:p-6 lg:p-8">
        {children}
      </main>
    </div>
  );
}

