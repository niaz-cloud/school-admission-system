import { Link } from 'react-router-dom';

export function NotFoundPage() {
  return (
    <div className="card">
      <h1>পৃষ্ঠাটি পাওয়া যায়নি</h1>
      <p>আপনি যে ঠিকানায় এসেছেন সেটি নেই।</p>
      <Link to="/" className="btn btn--primary">
        শ্রেণি তালিকায় ফিরে যান
      </Link>
    </div>
  );
}
