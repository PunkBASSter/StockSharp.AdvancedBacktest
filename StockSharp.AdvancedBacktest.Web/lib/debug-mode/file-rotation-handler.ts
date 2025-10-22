/**
 * File rotation handler for debug mode
 * Detects when backend rotates JSONL files and switches to next file
 */

/**
 * Detect if file has stopped growing (potential rotation)
 *
 * @param lastGrowthTime - Timestamp of last file growth (milliseconds)
 * @param thresholdMs - Time threshold to consider file stale (default: 5000ms)
 * @returns True if file appears to have stopped growing
 */
export function isFileStale(lastGrowthTime: number, thresholdMs: number = 5000): boolean {
  if (lastGrowthTime === 0) return false; // No data yet
  return Date.now() - lastGrowthTime > thresholdMs;
}

/**
 * Generate next file path in rotation sequence
 *
 * @param currentPath - Current file path (e.g., '/debug-mode/latest.jsonl')
 * @returns Next file path (e.g., '/debug-mode/latest_001.jsonl')
 *
 * @example
 * getNextRotatedFile('/debug-mode/latest.jsonl')      // -> '/debug-mode/latest_001.jsonl'
 * getNextRotatedFile('/debug-mode/latest_001.jsonl')  // -> '/debug-mode/latest_002.jsonl'
 * getNextRotatedFile('/debug-mode/latest_099.jsonl')  // -> '/debug-mode/latest_100.jsonl'
 */
export function getNextRotatedFile(currentPath: string): string {
  // Extract base path and extension
  const match = currentPath.match(/^(.+?)(_(\d+))?(\.[^.]+)$/);

  if (!match) {
    // No extension, append _001
    return `${currentPath}_001`;
  }

  const [, basePath, , currentNumber, extension] = match;

  // Parse current number (default to 0 if not present)
  const num = currentNumber ? parseInt(currentNumber, 10) : 0;

  // Increment and format with leading zeros (3 digits)
  const nextNum = num + 1;
  const formattedNum = nextNum.toString().padStart(3, '0');

  return `${basePath}_${formattedNum}${extension}`;
}

/**
 * Check if a file exists using HEAD request
 *
 * @param filePath - File path to check
 * @returns Promise resolving to true if file exists, false otherwise
 */
export async function fileExists(filePath: string): Promise<boolean> {
  try {
    const response = await fetch(filePath, { method: 'HEAD' });
    return response.ok; // 200-299 status codes
  } catch {
    return false;
  }
}

/**
 * Auto-discover next rotated file if current file has stopped growing
 *
 * @param currentPath - Current file path
 * @param lastGrowthTime - Timestamp of last file growth
 * @returns Promise resolving to next file path if found, or null if no rotation detected
 */
export async function discoverNextFile(
  currentPath: string,
  lastGrowthTime: number
): Promise<string | null> {
  // Check if file appears stale
  if (!isFileStale(lastGrowthTime)) {
    return null;
  }

  // Try to find next rotated file
  const nextPath = getNextRotatedFile(currentPath);
  const exists = await fileExists(nextPath);

  return exists ? nextPath : null;
}

/**
 * File rotation state tracker
 */
export class FileRotationTracker {
  private currentPath: string;
  private lastGrowthTime: number = 0;
  private onRotationCallback?: (newPath: string) => void;

  constructor(initialPath: string, onRotation?: (newPath: string) => void) {
    this.currentPath = initialPath;
    this.onRotationCallback = onRotation;
  }

  /**
   * Update last growth time (call this when file grows)
   */
  recordGrowth(): void {
    this.lastGrowthTime = Date.now();
  }

  /**
   * Check for rotation and auto-switch if detected
   * Call this periodically (e.g., every 5 seconds)
   */
  async checkRotation(): Promise<string | null> {
    const nextPath = await discoverNextFile(this.currentPath, this.lastGrowthTime);

    if (nextPath) {
      console.log(`File rotation detected: ${this.currentPath} -> ${nextPath}`);
      this.currentPath = nextPath;
      this.lastGrowthTime = Date.now(); // Reset growth time for new file

      if (this.onRotationCallback) {
        this.onRotationCallback(nextPath);
      }

      return nextPath;
    }

    return null;
  }

  /**
   * Get current file path
   */
  getCurrentPath(): string {
    return this.currentPath;
  }

  /**
   * Reset tracker to new path
   */
  reset(newPath: string): void {
    this.currentPath = newPath;
    this.lastGrowthTime = 0;
  }
}
