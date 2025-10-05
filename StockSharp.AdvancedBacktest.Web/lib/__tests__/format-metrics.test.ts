import { describe, it, expect } from '@jest/globals';
import {
  formatMetricValue,
  camelCaseToTitleCase,
  getMetricSentiment,
} from '../format-metrics';

describe('formatMetricValue', () => {
  describe('ratio formatting', () => {
    it('should format ratio to 2 decimal places', () => {
      expect(formatMetricValue(1.2345, 'ratio')).toBe('1.23');
      expect(formatMetricValue(2.5, 'ratio')).toBe('2.50');
      expect(formatMetricValue(-0.5678, 'ratio')).toBe('-0.57');
    });
  });

  describe('percentage formatting', () => {
    it('should format percentage with % symbol', () => {
      expect(formatMetricValue(45.67, 'percentage')).toBe('45.67%');
      expect(formatMetricValue(100, 'percentage')).toBe('100.00%');
      expect(formatMetricValue(-15.5, 'percentage')).toBe('-15.50%');
    });
  });

  describe('currency formatting', () => {
    it('should format currency with $ symbol and commas', () => {
      expect(formatMetricValue(1234.56, 'currency')).toBe('$1,234.56');
      expect(formatMetricValue(1000000, 'currency')).toBe('$1,000,000.00');
      expect(formatMetricValue(-500.25, 'currency')).toBe('-$500.25');
    });
  });

  describe('count formatting', () => {
    it('should format count as whole number with commas', () => {
      expect(formatMetricValue(1234, 'count')).toBe('1,234');
      expect(formatMetricValue(1000000, 'count')).toBe('1,000,000');
      expect(formatMetricValue(42, 'count')).toBe('42');
    });
  });

  describe('edge cases', () => {
    it('should handle null values', () => {
      expect(formatMetricValue(null, 'ratio')).toBe('N/A');
      expect(formatMetricValue(null, 'percentage')).toBe('N/A');
      expect(formatMetricValue(null, 'currency')).toBe('N/A');
      expect(formatMetricValue(null, 'count')).toBe('N/A');
    });

    it('should handle undefined values', () => {
      expect(formatMetricValue(undefined, 'ratio')).toBe('N/A');
      expect(formatMetricValue(undefined, 'percentage')).toBe('N/A');
      expect(formatMetricValue(undefined, 'currency')).toBe('N/A');
      expect(formatMetricValue(undefined, 'count')).toBe('N/A');
    });

    it('should handle string numbers', () => {
      expect(formatMetricValue('123.45', 'ratio')).toBe('123.45');
      expect(formatMetricValue('50', 'percentage')).toBe('50.00%');
    });

    it('should handle invalid string values', () => {
      expect(formatMetricValue('invalid', 'ratio')).toBe('N/A');
      expect(formatMetricValue('', 'percentage')).toBe('N/A');
    });

    it('should handle zero values', () => {
      expect(formatMetricValue(0, 'ratio')).toBe('0.00');
      expect(formatMetricValue(0, 'percentage')).toBe('0.00%');
      expect(formatMetricValue(0, 'currency')).toBe('$0.00');
      expect(formatMetricValue(0, 'count')).toBe('0');
    });
  });
});

describe('camelCaseToTitleCase', () => {
  it('should convert camelCase to Title Case', () => {
    expect(camelCaseToTitleCase('sharpeRatio')).toBe('Sharpe Ratio');
    expect(camelCaseToTitleCase('totalReturn')).toBe('Total Return');
    expect(camelCaseToTitleCase('maxDrawdown')).toBe('Max Drawdown');
    expect(camelCaseToTitleCase('winRate')).toBe('Win Rate');
  });

  it('should handle single word', () => {
    expect(camelCaseToTitleCase('ratio')).toBe('Ratio');
    expect(camelCaseToTitleCase('total')).toBe('Total');
  });

  it('should handle empty string', () => {
    expect(camelCaseToTitleCase('')).toBe('');
  });

  it('should handle already capitalized words', () => {
    expect(camelCaseToTitleCase('SharpeRatio')).toBe('Sharpe Ratio');
  });

  it('should handle multiple consecutive capitals', () => {
    expect(camelCaseToTitleCase('HTTPRequest')).toBe('H T T P Request');
  });
});

describe('getMetricSentiment', () => {
  it('should return positive for positive numbers', () => {
    expect(getMetricSentiment(1.5)).toBe('positive');
    expect(getMetricSentiment(100)).toBe('positive');
    expect(getMetricSentiment(0.01)).toBe('positive');
  });

  it('should return negative for negative numbers', () => {
    expect(getMetricSentiment(-1.5)).toBe('negative');
    expect(getMetricSentiment(-100)).toBe('negative');
    expect(getMetricSentiment(-0.01)).toBe('negative');
  });

  it('should return neutral for zero', () => {
    expect(getMetricSentiment(0)).toBe('neutral');
  });

  it('should return neutral for null/undefined', () => {
    expect(getMetricSentiment(null)).toBe('neutral');
    expect(getMetricSentiment(undefined)).toBe('neutral');
  });

  it('should handle string numbers', () => {
    expect(getMetricSentiment('5.5')).toBe('positive');
    expect(getMetricSentiment('-2.5')).toBe('negative');
    expect(getMetricSentiment('0')).toBe('neutral');
  });

  it('should return neutral for invalid strings', () => {
    expect(getMetricSentiment('invalid')).toBe('neutral');
    expect(getMetricSentiment('')).toBe('neutral');
  });
});
