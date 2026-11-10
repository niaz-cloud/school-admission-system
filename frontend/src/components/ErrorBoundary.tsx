import { Component, type ErrorInfo, type ReactNode } from 'react';

interface State {
  hasError: boolean;
}

/** Last line of defence: a rendering bug shows a friendly page instead of a blank screen. */
export class ErrorBoundary extends Component<{ children: ReactNode }, State> {
  state: State = { hasError: false };

  static getDerivedStateFromError(): State {
    return { hasError: true };
  }

  componentDidCatch(error: Error, info: ErrorInfo) {
    console.error('UI crashed', error, info.componentStack);
  }

  render() {
    if (!this.state.hasError) return this.props.children;
    return (
      <div className="page">
        <div className="card">
          <h1>কিছু একটা সমস্যা হয়েছে</h1>
          <p>পৃষ্ঠাটি দেখাতে সমস্যা হয়েছে। আবার চেষ্টা করুন।</p>
          <button type="button" className="btn btn--primary" onClick={() => window.location.reload()}>
            আবার চেষ্টা করুন
          </button>
        </div>
      </div>
    );
  }
}
