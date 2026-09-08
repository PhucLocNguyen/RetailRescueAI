import type { Metadata } from 'next';
import './globals.css';

export const metadata: Metadata = {
  title: 'RetailRescue AI | 小売食品廃棄防止＆スマートプロモーションAI',
  description: 'AI Agent for Retail Waste Prevention & Smart Promotion - ライフマート新宿店',
};

export default function RootLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return (
    <html lang="ja">
      <body className="antialiased min-h-screen flex flex-col bg-slate-50 text-slate-900">
        {children}
      </body>
    </html>
  );
}

