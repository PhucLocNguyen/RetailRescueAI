'use client';

import { useState, useRef, useEffect } from 'react';
import { sendChatMessage, createPromotion, approvePromotion } from '@/lib/api';
import {
  MessageSquareText,
  Send,
  Sparkles,
  Bot,
  User,
  CheckCircle2,
  Tag,
  ArrowRight,
  Clock,
} from 'lucide-react';

interface Message {
  role: 'user' | 'assistant';
  content: string;
  proposedPromotion?: any;
  agentSteps?: any[];
}

export default function ManagerChatbotPage() {
  const [messages, setMessages] = useState<Message[]>([
    {
      role: 'assistant',
      content:
        '佐藤店長、お疲れ様です！RetailRescue AI アシスタントです。\n現在庫データや賞味期限、売上予測に基づき、プロモーションの策定をお手伝いします。\n\n「たまごサンドを30%引きにしたい」「チキン弁当の在庫と割引を相談したい」「本日の危険な商品は？」など、何でもお気軽に相談してください。\n提案されたプロモーションは【店長承認】ボタンまたはチャットでの「承認します」で直ちにレジへ反映できます。',
    },
  ]);
  const [input, setInput] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const [creatingPromoName, setCreatingPromoName] = useState<string | null>(null);
  const [registeredPromoNames, setRegisteredPromoNames] = useState<Set<string>>(new Set());
  const [promoDbIds, setPromoDbIds] = useState<Record<string, number>>({});
  const [approvedPromoKeys, setApprovedPromoKeys] = useState<Set<string>>(new Set());
  const [isApprovingKey, setIsApprovingKey] = useState<string | null>(null);
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

      if (res.proposedPromotion) {
        const p = res.proposedPromotion;
        if (p.status === 'APPROVED') {
          setApprovedPromoKeys((prev) => new Set(prev).add(p.name));
          if (p.promotionId) {
            setPromoDbIds((prev) => ({ ...prev, [p.name]: p.promotionId }));
          }
        } else if (p.promotionId) {
          setPromoDbIds((prev) => ({ ...prev, [p.name]: p.promotionId }));
          setRegisteredPromoNames((prev) => new Set(prev).add(p.name));
        }
      }

      const assistantMsg: Message = {
        role: 'assistant',
        content: res.reply,
        proposedPromotion: res.proposedPromotion,
        agentSteps: res.agentSteps,
      };
      setMessages((prev) => [...prev, assistantMsg]);
    } catch (err: any) {
      setMessages((prev) => [
        ...prev,
        {
          role: 'assistant',
          content: `⚠️ 【AI処理エラー】\n${err.message || 'AI応答の取得中にエラーが発生しました。'}`,
        },
      ]);
    } finally {
      setIsLoading(false);
    }
  }

  async function handleCreateProposedPromo(promo: any) {
    const key = promo.name;
    if (registeredPromoNames.has(key) || creatingPromoName === key) return;

    setCreatingPromoName(key);
    try {
      const created = await createPromotion({
        name: promo.name,
        promotionType: promo.promotionType || 'DIRECT_DISCOUNT',
        targetProductId: promo.targetProductId,
        targetBatchId: promo.targetBatchId,
        discountPercent: promo.discountPercent,
        comboPrice: promo.comboPrice,
        startTime: promo.startTime || new Date().toISOString(),
        endTime: promo.endTime || new Date(Date.now() + 6 * 3600 * 1000).toISOString(),
        reasoning: promo.reasoning,
      });

      setPromoDbIds((prev) => ({ ...prev, [key]: created.id }));
      setRegisteredPromoNames((prev) => new Set(prev).add(key));

      setMessages((prev) => [
        ...prev,
        {
          role: 'assistant',
          content: `📝 プロモーション「${promo.name}」を【承認待ち（PENDING）】として登録しました（登録ID: #${created.id}）。\n下記の「店長承認」ボタンを押すか、チャットで「承認します」と送信することで直ちにPOSレジへ反映されます。`,
        },
      ]);
    } catch (err: any) {
      alert(`登録に失敗しました: ${err.message || err}`);
    } finally {
      setCreatingPromoName(null);
    }
  }

  async function handleApprovePromo(promo: any) {
    const key = promo.name;
    if (approvedPromoKeys.has(key) || isApprovingKey === key) return;

    setIsApprovingKey(key);
    try {
      let promoId = promo.promotionId || promoDbIds[key];

      // If not yet saved in DB, create it as PENDING first
      if (!promoId) {
        const created = await createPromotion({
          name: promo.name,
          promotionType: promo.promotionType || 'DIRECT_DISCOUNT',
          targetProductId: promo.targetProductId,
          targetBatchId: promo.targetBatchId,
          discountPercent: promo.discountPercent,
          comboPrice: promo.comboPrice,
          startTime: promo.startTime || new Date().toISOString(),
          endTime: promo.endTime || new Date(Date.now() + 6 * 3600 * 1000).toISOString(),
          reasoning: promo.reasoning,
        });
        promoId = created.id;
        setPromoDbIds((prev) => ({ ...prev, [key]: promoId }));
        setRegisteredPromoNames((prev) => new Set(prev).add(key));
      }

      // Execute manager approval
      await approvePromotion(promoId);

      setApprovedPromoKeys((prev) => new Set(prev).add(key));

      setMessages((prev) => [
        ...prev,
        {
          role: 'assistant',
          content: `🎉 【店長承認完了】\nプロモーション「${promo.name}」を正式に承認しました。\n\n• 対象ロット: ${promo.targetBatchCode || '指定ロット'} (${promo.targetProductName})\n• 割引率: ${promo.discountPercent}% OFF (¥${promo.discountedPrice?.toLocaleString()} / 定価: ¥${promo.originalPrice?.toLocaleString()})\n• ステータス: APPROVED（有効）\n\nSignalRを通じてPOSレジ端末へ即座に反映されました。レジで該当ロットをスキャンすると自動的に割引価格が適用されます。`,
        },
      ]);
    } catch (err: any) {
      alert(`承認に失敗しました: ${err.message || err}`);
    } finally {
      setIsApprovingKey(null);
    }
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

                {/* Collapsible Agent Collaboration Section (Microsoft Semantic Kernel) */}
                {!isUser && m.agentSteps && m.agentSteps.length > 0 && (
                  <div className="bg-slate-50 border border-slate-200 rounded-xl p-3 text-xs space-y-2 shadow-xs">
                    <div className="flex items-center justify-between font-bold text-slate-700">
                      <div className="flex items-center gap-1.5 text-indigo-700">
                        <Sparkles className="w-3.5 h-3.5 text-amber-500" />
                        <span>連携エージェント（Microsoft Semantic Kernel）:</span>
                      </div>
                      <span className="text-[10px] bg-indigo-100/70 text-indigo-800 px-2 py-0.5 rounded-md font-mono">
                        {m.agentSteps.length} エージェント協調
                      </span>
                    </div>

                    {/* Agent badges row */}
                    <div className="flex flex-wrap gap-1.5 pt-0.5">
                      {m.agentSteps.map((step: any, sIdx: number) => (
                        <span
                          key={sIdx}
                          className="inline-flex items-center gap-1 px-2 py-0.5 rounded-md bg-white border border-slate-200 text-[11px] font-medium text-slate-700 shadow-2xs"
                          title={step.roleTitle}
                        >
                          <span className="w-1.5 h-1.5 rounded-full bg-emerald-500"></span>
                          <span className="font-bold text-slate-900">{step.agentKey}</span>
                          <span className="text-[10px] text-slate-400 font-mono">({step.durationMs}ms)</span>
                        </span>
                      ))}
                    </div>

                    {/* Expandable trace details */}
                    <details className="text-[11px] text-slate-600 cursor-pointer pt-1">
                      <summary className="hover:text-indigo-600 font-medium text-slate-500 select-none">
                        各エージェントの分析詳細を表示 ▾
                      </summary>
                      <div className="mt-2 space-y-2 border-t border-slate-200 pt-2">
                        {m.agentSteps.map((step: any, sIdx: number) => (
                          <div key={sIdx} className="bg-white p-2 rounded-lg border border-slate-200 space-y-1">
                            <div className="flex justify-between items-center font-bold text-slate-800 text-[11px]">
                              <span>{step.agentName}</span>
                              <span className="text-emerald-600 text-[10px] font-mono px-1.5 py-0.2 bg-emerald-50 rounded">
                                {step.status}
                              </span>
                            </div>
                            <p className="text-[10px] text-slate-500">{step.description}</p>
                            {step.details && step.details.length > 0 && (
                              <ul className="mt-1 space-y-0.5 pl-3 list-disc text-[10px] text-slate-600">
                                {step.details.slice(0, 4).map((d: string, dIdx: number) => (
                                  <li key={dIdx}>{d}</li>
                                ))}
                              </ul>
                            )}
                          </div>
                        ))}
                      </div>
                    </details>
                  </div>
                )}

                {/* Interactive Promotion Proposal Card if generated */}
                {m.proposedPromotion && (() => {
                  const promo = m.proposedPromotion;
                  const key = promo.name;
                  const isApproved = approvedPromoKeys.has(key) || promo.status === 'APPROVED';
                  const isPending = (registeredPromoNames.has(key) || promo.promotionId) && !isApproved;

                  return (
                    <div className="bg-gradient-to-br from-indigo-50/90 via-white to-purple-50/90 border border-indigo-200 rounded-2xl p-4 shadow-sm space-y-3">
                      <div className="flex items-center justify-between">
                        <div className="flex items-center gap-1.5 text-xs font-bold text-indigo-900">
                          <Sparkles className="w-4 h-4 text-amber-500" />
                          <span>AIプロモーション提案プラン</span>
                        </div>
                        <span
                          className={`text-[10px] font-bold px-2 py-0.5 rounded-md ${
                            isApproved
                              ? 'bg-emerald-100 text-emerald-800 border border-emerald-300'
                              : isPending
                              ? 'bg-amber-100 text-amber-800 border border-amber-300'
                              : 'bg-indigo-100 text-indigo-800 border border-indigo-200'
                          }`}
                        >
                          {isApproved ? 'APPROVED (承認済み)' : isPending ? 'PENDING (承認待ち)' : 'PROPOSAL (提案中)'}
                        </span>
                      </div>

                      <div className="bg-white p-3 rounded-xl border border-indigo-100 text-xs space-y-1.5 shadow-2xs">
                        <p className="font-black text-slate-900 text-sm">{promo.name}</p>
                        <div className="flex justify-between text-slate-600 pt-1">
                          <span>対象商品 / ロット:</span>
                          <span className="font-medium text-slate-900">
                            {promo.targetProductName} <span className="text-slate-400 font-mono">({promo.targetBatchCode || '指定ロット'})</span>
                          </span>
                        </div>
                        <div className="flex justify-between text-slate-600">
                          <span>割引率:</span>
                          <span className="font-bold text-rose-600">{promo.discountPercent}% OFF</span>
                        </div>
                        <div className="flex justify-between text-slate-600">
                          <span>割引後価格:</span>
                          <span className="font-black text-slate-900">
                            ¥{promo.discountedPrice?.toLocaleString()} (定価: ¥{promo.originalPrice?.toLocaleString()})
                          </span>
                        </div>
                        <div className="flex justify-between text-slate-600">
                          <span>推奨時間帯:</span>
                          <span className="font-medium text-slate-800">夕方ピーク帯 (17:00〜22:00)</span>
                        </div>
                      </div>

                      {/* Action States */}
                      {isApproved ? (
                        <div className="p-3 bg-emerald-50 border border-emerald-300 rounded-xl flex items-center justify-between text-emerald-900 shadow-2xs">
                          <div className="flex items-center gap-2">
                            <CheckCircle2 className="w-5 h-5 text-emerald-600 shrink-0" />
                            <div>
                              <div className="font-black text-xs">🎉 【店長承認完了】レジへ即時反映済み</div>
                              <div className="text-[10px] text-emerald-700">ステータス: APPROVED | ロット別割引が有効になりました</div>
                            </div>
                          </div>
                          <a
                            href="/pos"
                            className="text-[11px] font-bold bg-emerald-600 hover:bg-emerald-500 text-white px-3 py-1.5 rounded-lg shadow transition flex items-center gap-1 shrink-0"
                          >
                            <span>POSレジで確認</span>
                            <ArrowRight className="w-3.5 h-3.5" />
                          </a>
                        </div>
                      ) : isPending ? (
                        <div className="space-y-2">
                          <div className="bg-amber-50 border border-amber-200 rounded-lg p-2.5 flex items-center justify-between text-xs text-amber-900">
                            <span className="flex items-center gap-1.5 font-bold">
                              <Clock className="w-3.5 h-3.5 text-amber-600" />
                              承認待ちリストに登録済み (ID: #{promoDbIds[key] || promo.promotionId})
                            </span>
                            <span className="text-[10px] bg-amber-200/70 text-amber-900 px-1.5 py-0.5 rounded font-mono font-bold">
                              PENDING
                            </span>
                          </div>
                          <button
                            onClick={() => handleApprovePromo(promo)}
                            disabled={isApprovingKey === key}
                            className="w-full py-2.5 font-bold text-xs rounded-xl shadow-md transition flex items-center justify-center gap-2 bg-gradient-to-r from-emerald-600 to-teal-600 hover:from-emerald-500 hover:to-teal-500 text-white"
                          >
                            <CheckCircle2 className="w-4 h-4" />
                            <span>
                              {isApprovingKey === key
                                ? '店長承認・配信処理中...'
                                : '【店長承認】このプロモーションを直ちに承認・有効化する'}
                            </span>
                          </button>
                        </div>
                      ) : (
                        <div className="space-y-2">
                          <button
                            onClick={() => handleApprovePromo(promo)}
                            disabled={isApprovingKey === key || creatingPromoName === key}
                            className="w-full py-2.5 font-bold text-xs rounded-xl shadow-md transition flex items-center justify-center gap-2 bg-gradient-to-r from-emerald-600 to-teal-600 hover:from-emerald-500 hover:to-teal-500 text-white"
                          >
                            <CheckCircle2 className="w-4 h-4" />
                            <span>
                              {isApprovingKey === key
                                ? '店長承認処理中...'
                                : '【店長承認】承認してレジへ即時反映 (Approve & Activate)'}
                            </span>
                          </button>

                          <button
                            onClick={() => handleCreateProposedPromo(promo)}
                            disabled={isApprovingKey === key || creatingPromoName === key}
                            className="w-full py-2 font-medium text-xs rounded-xl border border-slate-300 hover:bg-slate-50 text-slate-700 transition flex items-center justify-center gap-2 bg-white"
                          >
                            <Clock className="w-3.5 h-3.5 text-slate-500" />
                            <span>
                              {creatingPromoName === key ? '起票中...' : '「承認待ち（PENDING）」として一時保存する'}
                            </span>
                          </button>
                          <p className="text-[10px] text-center text-slate-500">
                            ※ 店長が承認した時点で、SignalRによりレジ（POS）へリアルタイム配信されます。
                          </p>
                        </div>
                      )}
                    </div>
                  );
                })()}
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
          'このプロモーションを承認します',
          'たまごサンドを30%引きにしたい',
          'チキン南蛮弁当の在庫状況と割引を提案して',
          '本日の賞味期限切迫商品はどれですか？',
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

