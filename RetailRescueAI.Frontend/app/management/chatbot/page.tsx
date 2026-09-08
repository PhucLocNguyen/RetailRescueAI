'use client';

import { useState, useRef, useEffect } from 'react';
import { sendChatMessage } from '@/lib/api';
import {
  MessageSquareText,
  Send,
  Sparkles,
  Bot,
  User,
  CheckCircle2,
  Tag,
  ArrowRight,
} from 'lucide-react';

interface Message {
  role: 'user' | 'assistant';
  content: string;
  proposedPromotion?: any;
}

export default function ManagerChatbotPage() {
  const [messages, setMessages] = useState<Message[]>([
    {
      role: 'assistant',
      content:
        '佐藤店長、お疲れ様です！RetailRescue AI アシスタントです。\n現在庫データや賞味期限、売上予測に基づき、プロモーションの策定をお手伝いします。\n\n「チキン弁当の在庫と割引を相談したい」「本日の危険な商品は？」など、何でもお気軽に相談してください。',
    },
  ]);
  const [input, setInput] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const messagesEndRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [messages]);

  async function handleSend(textToSend?: string) {
    const messageContent = textToSend || input;
    if (!messageContent.trim() || isLoading) return;

    const userMsg: Message = { role: 'user', content: messageContent };
    setMessages((prev) => [...prev, userMsg]);
    if (!textToSend) setInput('');
    setIsLoading(true);

    try {
      const history = messages.map((m) => ({
        role: m.role,
        content: m.content,
        timestamp: new Date().toISOString(),
      }));

      const res = await sendChatMessage(messageContent, history);
      const assistantMsg: Message = {
        role: 'assistant',
        content: res.reply,
        proposedPromotion: res.proposedPromotion,
      };
      setMessages((prev) => [...prev, assistantMsg]);
    } catch (err) {
      setMessages((prev) => [
        ...prev,
        {
          role: 'assistant',
          content: '申し訳ありません。AI応答の取得中にエラーが発生しました。',
        },
      ]);
    } finally {
      setIsLoading(false);
    }
  }

  async function handleCreateProposedPromo(promo: any) {
    alert(
      `提案「${promo.name}」を【承認待ち（PENDING）】として登録しました。\n「AIプロモーション提案」または「プロモーション管理」画面で内容をご確認の上、承認してください。`
    );
  }

  return (
    <div className="max-w-4xl mx-auto space-y-4 flex flex-col h-[calc(100vh-160px)]">
      {/* Header */}
      <div className="bg-white rounded-2xl p-5 border border-slate-200 shadow-xs flex items-center justify-between">
        <div className="flex items-center gap-3">
          <div className="w-10 h-10 rounded-xl bg-indigo-100 text-indigo-600 flex items-center justify-center shadow-xs">
            <Bot className="w-6 h-6" />
          </div>
          <div>
            <h1 className="font-black text-lg text-slate-900 flex items-center gap-2">
              <span>店長専属 AIアシスタント</span>
              <span className="text-[10px] bg-emerald-100 text-emerald-800 font-bold px-2 py-0.5 rounded-full">
                リアルタイム対話中
              </span>
            </h1>
            <p className="text-xs text-slate-500">
              自然言語で在庫・期限・割引施策を相談し、プロモーション提案を即座に起票できます
            </p>
          </div>
        </div>
      </div>

      {/* Chat Messages Container */}
      <div className="flex-1 bg-white rounded-2xl border border-slate-200 shadow-sm p-5 overflow-y-auto space-y-4">
        {messages.map((m, idx) => {
          const isUser = m.role === 'user';
          return (
            <div
              key={idx}
              className={`flex gap-3 max-w-[85%] ${isUser ? 'ml-auto flex-row-reverse' : ''}`}
            >
              <div
                className={`w-8 h-8 rounded-lg flex items-center justify-center shrink-0 shadow-xs ${
                  isUser ? 'bg-indigo-600 text-white' : 'bg-slate-100 text-indigo-600'
                }`}
              >
                {isUser ? <User className="w-4 h-4" /> : <Bot className="w-4 h-4" />}
              </div>

              <div className="space-y-3">
                <div
                  className={`p-4 rounded-2xl text-xs sm:text-sm leading-relaxed whitespace-pre-line ${
                    isUser
                      ? 'bg-indigo-600 text-white rounded-tr-none'
                      : 'bg-slate-50 text-slate-800 border border-slate-200 rounded-tl-none'
                  }`}
                >
                  {m.content}
                </div>

                {/* Interactive Promotion Proposal Card if generated */}
                {m.proposedPromotion && (
                  <div className="bg-gradient-to-br from-indigo-50 to-purple-50 border border-indigo-200 rounded-2xl p-4 shadow-sm space-y-3">
                    <div className="flex items-center gap-1.5 text-xs font-bold text-indigo-900">
                      <Sparkles className="w-4 h-4 text-amber-500" />
                      <span>AIプロモーション提案プラン</span>
                    </div>

                    <div className="bg-white p-3 rounded-xl border border-indigo-100 text-xs space-y-1.5">
                      <p className="font-black text-slate-900 text-sm">{m.proposedPromotion.name}</p>
                      <div className="flex justify-between text-slate-600 pt-1">
                        <span>割引率:</span>
                        <span className="font-bold text-rose-600">{m.proposedPromotion.discountPercent}% OFF</span>
                      </div>
                      <div className="flex justify-between text-slate-600">
                        <span>割引後価格:</span>
                        <span className="font-black text-slate-900">
                          ¥{m.proposedPromotion.discountedPrice?.toLocaleString()} (定価: ¥{m.proposedPromotion.originalPrice?.toLocaleString()})
                        </span>
                      </div>
                      <div className="flex justify-between text-slate-600">
                        <span>推奨時間帯:</span>
                        <span className="font-medium text-slate-800">夕方ピーク帯 (17:00〜22:00)</span>
                      </div>
                    </div>

                    <button
                      onClick={() => handleCreateProposedPromo(m.proposedPromotion)}
                      className="w-full py-2.5 bg-indigo-600 hover:bg-indigo-500 text-white font-bold text-xs rounded-xl shadow transition flex items-center justify-center gap-2"
                    >
                      <CheckCircle2 className="w-4 h-4" />
                      <span>このプロモーションを「承認待ち」として登録する</span>
                    </button>
                    <p className="text-[10px] text-center text-slate-500">
                      ※ AIは直接有効化しません。店長ポータルで承認後にレジへ反映されます。
                    </p>
                  </div>
                )}
              </div>
            </div>
          );
        })}
        {isLoading && (
          <div className="flex gap-3 max-w-[80%]">
            <div className="w-8 h-8 rounded-lg bg-slate-100 text-indigo-600 flex items-center justify-center shrink-0">
              <Bot className="w-4 h-4" />
            </div>
            <div className="p-4 rounded-2xl bg-slate-50 text-slate-500 text-xs border border-slate-200 animate-pulse">
              AIが在庫と販売予測データを分析中...
            </div>
          </div>
        )}
        <div ref={messagesEndRef} />
      </div>

      {/* Suggested Quick Prompt Chips */}
      <div className="flex flex-wrap gap-2 text-xs">
        {[
          'チキン南蛮弁当の在庫状況と割引を提案して',
          '本日の賞味期限切迫商品はどれですか？',
          '夕方のピーク時間帯に最適なプロモーションは？',
        ].map((chip, idx) => (
          <button
            key={idx}
            onClick={() => handleSend(chip)}
            className="bg-white hover:bg-indigo-50 text-indigo-700 border border-indigo-200 px-3 py-1.5 rounded-full text-xs font-medium transition shadow-2xs"
          >
            {chip}
          </button>
        ))}
      </div>

      {/* Message Input Box */}
      <div className="bg-white p-3 rounded-2xl border border-slate-200 shadow-sm flex items-center gap-2">
        <input
          type="text"
          placeholder="AIアシスタントにメッセージを入力（例: チキン弁当を20%引きにしたい）..."
          value={input}
          onChange={(e) => setInput(e.target.value)}
          onKeyDown={(e) => {
            if (e.key === 'Enter') handleSend();
          }}
          className="flex-1 px-4 py-2 text-xs sm:text-sm focus:outline-none"
        />
        <button
          disabled={!input.trim() || isLoading}
          onClick={() => handleSend()}
          className="p-3 bg-indigo-600 hover:bg-indigo-500 disabled:bg-slate-300 text-white rounded-xl transition shadow"
        >
          <Send className="w-4 h-4" />
        </button>
      </div>
    </div>
  );
}

