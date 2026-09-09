'use client';

import { useState, useEffect } from 'react';
import {
  Sparkles,
  Clock,
  TrendingUp,
  Brain,
  ShieldCheck,
  CheckCircle2,
  Cpu,
  ArrowRight,
  X,
  Play,
  Check,
  AlertCircle,
  Zap,
} from 'lucide-react';
import { AiAgentTraceStep, triggerManualAiRun, AiPipelineRunResponse } from '@/lib/api';

interface AiAgentWorkflowModalProps {
  isOpen: boolean;
  onClose: () => void;
  onComplete?: () => void;
}

const AGENT_CONFIGS: Record<string, { icon: any; color: string; bg: string; borderColor: string; glow: string }> = {
  ExpiryAgent: {
    icon: Clock,
    color: 'text-amber-500',
    bg: 'bg-amber-500/10',
    borderColor: 'border-amber-500/30',
    glow: 'shadow-amber-500/20',
  },
  SalesAgent: {
    icon: TrendingUp,
    color: 'text-blue-500',
    bg: 'bg-blue-500/10',
    borderColor: 'border-blue-500/30',
    glow: 'shadow-blue-500/20',
  },
  PromotionAgent: {
    icon: Brain,
    color: 'text-purple-500',
    bg: 'bg-purple-500/10',
    borderColor: 'border-purple-500/30',
    glow: 'shadow-purple-500/20',
  },
  ReviserAgent: {
    icon: ShieldCheck,
    color: 'text-emerald-500',
    bg: 'bg-emerald-500/10',
    borderColor: 'border-emerald-500/30',
    glow: 'shadow-emerald-500/20',
  },
  OrchestratorAgent: {
    icon: Cpu,
    color: 'text-rose-500',
    bg: 'bg-rose-500/10',
    borderColor: 'border-rose-500/30',
    glow: 'shadow-rose-500/20',
  },
};

export default function AiAgentWorkflowModal({ isOpen, onClose, onComplete }: AiAgentWorkflowModalProps) {
  const [isRunning, setIsRunning] = useState(false);
  const [activeStepIndex, setActiveStepIndex] = useState<number>(0);
  const [pipelineData, setPipelineData] = useState<AiPipelineRunResponse | null>(null);
  const [completedSteps, setCompletedSteps] = useState<number[]>([]);

  useEffect(() => {
    if (isOpen) {
      startPipelineRun();
    } else {
      setPipelineData(null);
      setCompletedSteps([]);
      setActiveStepIndex(0);
    }
  }, [isOpen]);

  async function startPipelineRun() {
    setIsRunning(true);
    setCompletedSteps([]);
    setActiveStepIndex(0);

    try {
      const response = await triggerManualAiRun();
      setPipelineData(response);

      // Animate through steps for demo visual experience
      for (let i = 0; i < response.steps.length; i++) {
        setActiveStepIndex(i);
        await new Promise((r) => setTimeout(r, 650));
        setCompletedSteps((prev) => [...prev, i]);
      }
    } catch (err) {
      console.error(err);
    } finally {
      setIsRunning(false);
    }
  }

  if (!isOpen) return null;

  const steps = pipelineData?.steps || [];
  const currentStep = steps[activeStepIndex];

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-950/80 backdrop-blur-md animate-in fade-in duration-200">
      <div className="bg-slate-900 border border-slate-700/80 w-full max-w-4xl rounded-3xl shadow-2xl overflow-hidden text-slate-100 flex flex-col max-h-[90vh]">
        {/* Header */}
        <div className="p-6 border-b border-slate-800 bg-slate-950/50 flex items-center justify-between">
          <div className="space-y-1">
            <div className="flex items-center gap-2">
              <span className="inline-flex items-center gap-1 text-[11px] font-bold text-purple-400 bg-purple-950/60 border border-purple-800 px-2.5 py-0.5 rounded-full uppercase tracking-wider">
                <Sparkles className="w-3 h-3 text-purple-400" />
                Google Gemini 2.5 Flash ＆ Multi-Agent
              </span>
              <span className="text-[11px] font-mono text-slate-400 bg-slate-800 px-2 py-0.5 rounded-full">
                .NET 9 協調分散アーキテクチャ
              </span>
            </div>
            <h2 className="text-xl font-black text-white flex items-center gap-2">
              <span>AIマルチエージェント協調分析・パイプライン実演デモ</span>
            </h2>
            <p className="text-xs text-slate-400">
              各専門エージェントが自律的に連携し、在庫監視からGemini販促立案・安全制約検証までを順次実行
            </p>
          </div>

          <button
            onClick={onClose}
            className="p-2 rounded-xl text-slate-400 hover:text-white hover:bg-slate-800 transition"
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        {/* Content Body */}
        <div className="p-6 space-y-6 overflow-y-auto flex-1">
          {/* 5-Node Interactive Workflow Diagram */}
          <div className="bg-slate-950/60 p-4 rounded-2xl border border-slate-800">
            <div className="text-[11px] font-bold uppercase tracking-wider text-slate-400 mb-3 flex items-center justify-between">
              <span className="flex items-center gap-1.5">
                <Zap className="w-3.5 h-3.5 text-amber-400" /> エージェント協調パイプライン・フロー
              </span>
              <span className="font-mono text-slate-500">
                {completedSteps.length} / {steps.length || 5} 完了
              </span>
            </div>

            <div className="grid grid-cols-2 md:grid-cols-5 gap-2.5 relative">
              {['ExpiryAgent', 'SalesAgent', 'PromotionAgent', 'ReviserAgent', 'OrchestratorAgent'].map(
                (agentKey, idx) => {
                  const step = steps[idx];
                  const cfg = AGENT_CONFIGS[agentKey] || AGENT_CONFIGS.ExpiryAgent;
                  const Icon = cfg.icon;
                  const isCompleted = completedSteps.includes(idx);
                  const isActive = activeStepIndex === idx;

                  return (
                    <div
                      key={agentKey}
                      onClick={() => step && setActiveStepIndex(idx)}
                      className={`cursor-pointer rounded-2xl p-3 border transition flex flex-col justify-between relative overflow-hidden ${
                        isActive
                          ? `bg-slate-800 border-purple-500 ring-2 ring-purple-500/40 shadow-lg ${cfg.glow}`
                          : isCompleted
                          ? 'bg-slate-800/40 border-emerald-500/40 hover:bg-slate-800'
                          : 'bg-slate-900/50 border-slate-800 opacity-60'
                      }`}
                    >
                      {isActive && isRunning && (
                        <div className="absolute inset-0 bg-gradient-to-r from-transparent via-purple-500/10 to-transparent animate-pulse" />
                      )}

                      <div className="flex items-center justify-between mb-2">
                        <div className={`w-8 h-8 rounded-xl ${cfg.bg} ${cfg.color} flex items-center justify-center`}>
                          <Icon className="w-4 h-4" />
                        </div>
                        {isCompleted ? (
                          <div className="w-5 h-5 rounded-full bg-emerald-500 text-slate-950 flex items-center justify-center font-bold text-[10px]">
                            <Check className="w-3 h-3" />
                          </div>
                        ) : isActive && isRunning ? (
                          <div className="w-2.5 h-2.5 rounded-full bg-amber-400 animate-ping" />
                        ) : (
                          <span className="text-[10px] font-mono text-slate-500">Step {idx + 1}</span>
                        )}
                      </div>

                      <div>
                        <span className="text-[10px] font-mono text-slate-400 block">{agentKey}</span>
                        <h4 className="text-xs font-bold text-white line-clamp-1">
                          {step?.agentName || (idx === 0 ? '賞味期限監視' : idx === 1 ? '販売速度試算' : idx === 2 ? 'Gemini販促立案' : idx === 3 ? '安全制約検証' : '統括統合')}
                        </h4>
                      </div>

                      {step && (
                        <div className="mt-2 pt-2 border-t border-slate-800 flex justify-between items-center text-[10px] text-slate-400 font-mono">
                          <span>処理時間</span>
                          <span className="text-purple-400 font-bold">{step.durationMs}ms</span>
                        </div>
                      )}
                    </div>
                  );
                }
              )}
            </div>
          </div>

          {/* Active Step Real-time Inspection Card */}
          {currentStep ? (
            <div className="bg-slate-950/70 border border-slate-800 rounded-2xl p-5 space-y-4">
              <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-2 pb-3 border-b border-slate-800">
                <div className="flex items-center gap-2.5">
                  <div className="p-2 rounded-xl bg-purple-500/20 text-purple-400 border border-purple-500/30">
                    <Brain className="w-5 h-5" />
                  </div>
                  <div>
                    <div className="flex items-center gap-2">
                      <span className="font-mono text-xs text-purple-400 font-bold">{currentStep.agentKey}</span>
                      <span className="text-[11px] text-slate-400">({currentStep.roleTitle})</span>
                    </div>
                    <h3 className="text-base font-black text-white">{currentStep.agentName}</h3>
                  </div>
                </div>

                <div className="flex items-center gap-2 text-xs">
                  <span className="px-2.5 py-1 rounded-full bg-emerald-500/20 text-emerald-400 border border-emerald-500/30 font-bold">
                    実行状態: 正常完了 (PASS)
                  </span>
                  <span className="px-2.5 py-1 rounded-full bg-slate-800 text-slate-300 font-mono">
                    所要: {currentStep.durationMs}ms
                  </span>
                </div>
              </div>

              <p className="text-xs text-slate-300 leading-relaxed">{currentStep.description}</p>

              {/* Terminal Log Output */}
              <div className="bg-black/80 rounded-xl p-4 border border-slate-800 font-mono text-xs space-y-1.5 overflow-x-auto shadow-inner">
                <div className="text-[11px] text-slate-500 pb-1 border-b border-slate-800 flex items-center gap-1.5">
                  <div className="w-2 h-2 rounded-full bg-emerald-500" />
                  <span>エージェント処理ログ ＆ 算出データ:</span>
                </div>
                {currentStep.details.map((line, lIdx) => (
                  <div key={lIdx} className="text-slate-200 leading-relaxed flex items-start gap-2">
                    <span className="text-purple-400 select-none">❯</span>
                    <span>{line}</span>
                  </div>
                ))}
              </div>
            </div>
          ) : (
            <div className="py-16 text-center text-slate-500 text-sm">
              <Sparkles className="w-8 h-8 text-purple-500 mx-auto mb-2 animate-spin" />
              <span>AIエージェント分析パイプラインを初期化中...</span>
            </div>
          )}

          {/* Success Summary Banner */}
          {!isRunning && pipelineData && (
            <div className="bg-gradient-to-r from-emerald-950/40 via-purple-950/40 to-slate-900 border border-emerald-500/40 rounded-2xl p-4 flex flex-col sm:flex-row items-center justify-between gap-4">
              <div className="flex items-center gap-3">
                <div className="w-10 h-10 rounded-xl bg-emerald-500 text-slate-950 flex items-center justify-center font-black">
                  <CheckCircle2 className="w-6 h-6" />
                </div>
                <div>
                  <h4 className="font-bold text-sm text-white">AIパイプライン全行程の完了</h4>
                  <p className="text-xs text-slate-300">
                    新規に <strong className="text-emerald-400 font-black">{pipelineData.createdCount}件</strong> のプロモーション提案を「店長承認待ち (PENDING)」としてキューに登録しました。
                  </p>
                </div>
              </div>

              <div className="flex items-center gap-2">
                <button
                  onClick={startPipelineRun}
                  className="px-3.5 py-2 rounded-xl bg-slate-800 hover:bg-slate-700 text-slate-300 text-xs font-bold transition flex items-center gap-1.5"
                >
                  <Play className="w-3.5 h-3.5" /> 再実行
                </button>
                {onComplete && (
                  <button
                    onClick={() => {
                      onClose();
                      onComplete();
                    }}
                    className="px-5 py-2.5 rounded-xl bg-emerald-500 hover:bg-emerald-400 text-slate-950 font-black text-xs shadow-lg transition flex items-center gap-1.5"
                  >
                    <span>提案一覧で割引率を調整・承認する</span>
                    <ArrowRight className="w-4 h-4" />
                  </button>
                )}
              </div>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}

