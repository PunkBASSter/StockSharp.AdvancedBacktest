export default function LoadingState() {
  return (
    <div className="animate-pulse space-y-4 p-8">
      <div className="h-8 bg-gray-200 rounded w-1/4"></div>
      <div className="h-96 bg-gray-200 rounded"></div>
      <div className="grid grid-cols-3 gap-4">
        <div className="h-24 bg-gray-200 rounded"></div>
        <div className="h-24 bg-gray-200 rounded"></div>
        <div className="h-24 bg-gray-200 rounded"></div>
      </div>
    </div>
  );
}
