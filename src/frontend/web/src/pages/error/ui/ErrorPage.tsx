import React, {useState, useEffect} from "react";
import {useRouteError, isRouteErrorResponse, useNavigate} from "react-router";
import {AlertCircle, FileQuestion} from "lucide-react";
import {Button} from "@/shared/ui/button";
import {DvdBouncer} from "@/shared/ui/dvd-bouncer";

export function ErrorPage() {
    const [showMicrowave] = useState(() => Math.random() > 0.5);
    const error = useRouteError();
    const navigate = useNavigate();

    let title = "Oops! Something went wrong.";
    let message = "An unexpected error occurred. Our team has been notified.";
    let Icon = AlertCircle;

    if (isRouteErrorResponse(error) && error.status === 404) {
        title = "404 - Page Not Found";
        message = "The page you are looking for doesn't exist or has been moved.";
        Icon = FileQuestion;
    }

    if (showMicrowave) {
        return <MicrowaveErrorView title={title} message={message} onBack={() => navigate(-1)} />;
    }

    return <StandardErrorView title={title} message={message} Icon={Icon} onBack={() => navigate(-1)} />;
}

function StandardErrorView({ title, message, Icon, onBack }: any) {
    return (
        <div className="relative flex min-h-screen flex-col items-center justify-center bg-background p-4 text-center overflow-hidden">
            <DvdBouncer icon={Icon} />
            <div className="relative z-10 flex max-w-md flex-col items-center gap-6">
                <div className="rounded-full bg-muted p-6">
                    <Icon className="h-12 w-12 text-muted-foreground" />
                </div>
                <div className="space-y-2">
                    <h1 className="text-3xl font-bold tracking-tight">{title}</h1>
                    <p className="text-muted-foreground">{message}</p>
                </div>
                <Button onClick={onBack} className="mt-4">Go Back</Button>
            </div>
        </div>
    );
}

type Stage = 'reset' | 'sliding_in' | 'cooking' | 'exploded';

function MicrowaveErrorView({ title, message, onBack }: any) {
    const [stage, setStage] = useState<Stage>('reset');

    useEffect(() => {
        let timeouts: number[] = [];
        const schedule = (cb: () => void, delay: number) => {
            timeouts.push(window.setTimeout(cb, delay));
        };

        const runCycle = () => {
            setStage('reset');
            schedule(() => setStage('sliding_in'), 100);
            schedule(() => setStage('cooking'), 1200);
            schedule(() => setStage('exploded'), 4200);
            schedule(runCycle, 7200);
        };

        runCycle();
        return () => timeouts.forEach(clearTimeout);
    }, []);

    const isCooking = stage === 'cooking';
    const isExploded = stage === 'exploded';

    const getHddStyles = (): React.CSSProperties => {
        const centerLeft = '80px';
        const centerTop = '55px';

        switch(stage) {
            case 'reset':
                return { left: '-150px', top: centerTop, opacity: 1, transition: 'none' };

            case 'sliding_in':
            case 'cooking':
                return { left: centerLeft, top: centerTop, opacity: 1, transition: 'left 1000ms cubic-bezier(0.2, 0.8, 0.2, 1)' };

            case 'exploded':
                return { left: centerLeft, top: centerTop, opacity: 0, transition: 'opacity 100ms' };
            default:
                return { opacity: 0 };
        }
    };

    const shrapnels = [
        { id: 1, x: '-60vw', y: '-50vh', rot: '210deg', size: 'w-10 h-16 bg-gray-700' },
        { id: 2, x: '60vw', y: '-40vh', rot: '45deg', size: 'w-14 h-14 bg-gray-800' },
        { id: 3, x: '-40vw', y: '50vh', rot: '-60deg', size: 'w-6 h-20 bg-gray-600' },
        { id: 4, x: '40vw', y: '40vh', rot: '90deg', size: 'w-12 h-6 bg-gray-400' },
        { id: 5, x: '-10vw', y: '-70vh', rot: '180deg', size: 'w-8 h-8 bg-black' },
        { id: 6, x: '20vw', y: '-60vh', rot: '-45deg', size: 'w-16 h-10 bg-gray-700' },
    ];

    return (
        <div className="flex flex-col items-center justify-center min-h-screen bg-background overflow-hidden relative">
            <div className="absolute top-10 text-center space-y-2 z-10 p-4 bg-background/50 backdrop-blur-sm rounded-xl">
                <h1 className="text-3xl font-bold tracking-tight">{title}</h1>
                <p className="text-muted-foreground max-w-md">{message}</p>
                <Button variant="outline" onClick={onBack} className="mt-4">Go Back</Button>
            </div>

            <style>
                {`
                @keyframes custom-shake {
                    0%, 100% { transform: translate(0, 0); }
                    25% { transform: translate(-2px, 2px); }
                    50% { transform: translate(2px, -2px); }
                    75% { transform: translate(-2px, 2px); }
                }
                .animate-custom-shake { animation: custom-shake 0.1s infinite; }
                `}
            </style>

            <div className="absolute inset-0 w-full h-full pointer-events-none">
                {shrapnels.map(s => (
                    <div key={s.id}
                         className={`absolute z-50 rounded-sm ${s.size} shadow-2xl`}
                         style={{
                             left: '50%', top: '50%',
                             transform: isExploded ? `translate(calc(-50% + ${s.x}), calc(-50% + ${s.y})) rotate(${s.rot})` : 'translate(-50%, -50%) rotate(0deg)',
                             opacity: isExploded ? 1 : 0,
                             transition: stage === 'reset' ? 'none' : 'transform 1000ms ease-out, opacity 1000ms ease-out'
                         }}
                    />
                ))}

                <div className="absolute left-1/2 top-1/2 -translate-x-1/2 -translate-y-1/2 z-60 w-10 h-10">
                    <div className="absolute inset-0 rounded-full bg-red-600 blur-3xl flex items-center justify-center pointer-events-none origin-center transform-gpu"
                         style={{
                             transform: isExploded ? 'scale(60)' : 'scale(0)',
                             opacity: isExploded ? 0 : (stage === 'reset' ? 0 : 1),
                             transition: stage === 'reset' ? 'none' : 'transform 700ms ease-out, opacity 700ms ease-out'
                         }}
                    >
                        <div className="w-full h-full bg-yellow-300 rounded-full blur-2xl" />
                    </div>
                </div>

                <div className="absolute left-1/2 top-1/2 z-20 transform-gpu"
                     style={{
                         transform: isExploded ? 'translate(-50%, -50%) scale(0.5)' : 'translate(-50%, -50%) scale(1)',
                         opacity: isExploded ? 0 : 1,
                         transition: stage === 'reset' ? 'none' : 'transform 300ms ease-out, opacity 300ms ease-out'
                     }}
                >
                    <div className={`relative w-[320px] h-50 rounded-xl border-8 border-gray-300 shadow-xl overflow-hidden transform-gpu ${isCooking ? 'bg-orange-900/50 animate-custom-shake' : 'bg-gray-800'}`}
                         style={{ transition: stage === 'reset' ? 'none' : 'background-color 300ms' }}>
                        <div className="absolute inset-0 bg-yellow-500/30 z-0" style={{ opacity: isCooking ? 1 : 0, transition: stage === 'reset' ? 'none' : 'opacity 300ms' }} />
                        <div className="absolute z-10 flex flex-col items-center p-1 bg-gray-200 border-2 border-gray-400 shadow-lg rounded transform-gpu" style={{ width: '70px', height: '90px', ...getHddStyles() }}>
                            <div className="w-12 h-12 rounded-full bg-gray-700 border-2 border-gray-300 relative flex items-center justify-center shadow-inner mt-1">
                                <div className={`w-4 h-4 rounded-full bg-gray-400 flex items-center justify-center ${isCooking ? 'animate-[spin_0.1s_linear_infinite]' : ''}`}>
                                    <div className="w-1 h-1 rounded-full bg-gray-800 absolute top-0.5" />
                                    <div className="w-1 h-1 rounded-full bg-gray-800 absolute bottom-0.5" />
                                </div>
                            </div>
                            <div className="w-14 h-4 bg-white mt-auto border border-gray-300 rounded-sm text-[6px] text-center p-0.5 text-gray-500 font-bold leading-none">
                                HDD
                            </div>
                        </div>

                        <div className="absolute left-0 top-0 w-57.5 h-full bg-blue-900/30 border-r-8 border-gray-300 z-20 flex items-center justify-center">
                            <div className="w-full h-full opacity-20 bg-[radial-gradient(circle,black_1px,transparent_1px)] bg-size-[6px_6px]" />
                        </div>

                        <div className="absolute right-0 top-0 w-18.5 h-full bg-gray-200 border-l-4 border-gray-400 z-30 flex flex-col items-center py-4 gap-2">
                            <div className={`w-14 h-8 bg-black rounded flex items-center justify-center text-red-500 font-mono text-sm shadow-inner transition-colors ${isCooking ? 'text-red-400' : 'text-gray-800'}`}>
                                {isCooking ? 'ERR' : '404'}
                            </div>
                            <div className="grid grid-cols-2 gap-2 mt-2">
                                {[...Array(6)].map((_, i) => <div key={i} className="w-4 h-4 bg-gray-400 rounded-full shadow-sm" />)}
                            </div>
                            <div className={`w-10 h-10 mt-auto rounded-full shadow-md border-2 border-gray-400 ${isCooking ? 'bg-green-600' : 'bg-green-500'}`} />
                        </div>
                    </div>
                </div>

                {isExploded && (
                    <div className="absolute left-1/2 top-1/2 -translate-x-1/2 -translate-y-1/2 w-[320px] h-50 bg-gray-900 rounded-xl shadow-2xl z-10 border border-gray-800 overflow-hidden">
                        <div className="w-full h-full opacity-50 bg-[radial-gradient(ellipse_at_center,black_0%,transparent_100%)]" />
                        <div className="absolute bottom-0 right-0 w-12 h-4 bg-gray-800 rotate-12 translate-y-2 translate-x-2" />
                    </div>
                )}
            </div>
        </div>
    );
}