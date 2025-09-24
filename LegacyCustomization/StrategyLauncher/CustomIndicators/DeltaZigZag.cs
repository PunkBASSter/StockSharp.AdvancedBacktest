using System;
using System.ComponentModel.DataAnnotations;
using Ecng.Collections;
using Ecng.Serialization;
using StockSharp.Localization;

namespace StockSharp.Algo.Indicators;

/// <summary>
/// Delta Zig Zag.
/// </summary>
[Display(
	ResourceType = typeof(LocalizedStrings),
	Name = "Delta ZigZag",
	Description = "Delta ZigZag")]
[IndicatorIn(typeof(CandleIndicatorValue))]
[IndicatorOut(typeof(ZigZagIndicatorValue))]
public class DeltaZigZag : BaseIndicator
{
	private readonly CircularBufferEx<decimal> _buffer = new(2);
	private decimal? _lastExtremum;
	private int _shift;
	private bool? _isUpTrend;

	/// <summary>
	/// Initializes a new instance of the <see cref="DeltaZigZag"/>.
	/// </summary>
	public DeltaZigZag()
	{
	}

	private decimal _delta = 0.001m;

	/// <summary>
	/// Price change.
	/// </summary>
	[Display(
		ResourceType = typeof(LocalizedStrings),
		Name = "Delta",
		Description = "Price change.",
		GroupName = LocalizedStrings.GeneralKey)]
	public decimal Delta
	{
		get => _delta;
		set
		{
			if (value <= 0)
				throw new ArgumentOutOfRangeException(nameof(value));

			if (_delta == value)
				return;

			_delta = value;
			Reset();
		}
	}

	/// <inheritdoc />
	public override int NumValuesToInitialize => _buffer.Capacity;

	/// <inheritdoc />
	protected override bool CalcIsFormed() => _buffer.Capacity == _buffer.Count;

	/// <inheritdoc />
	public override void Reset()
	{
		_buffer.Clear();
		_lastExtremum = default;
		_shift = default;
		_isUpTrend = default;

		base.Reset();
	}

	/// <inheritdoc />
	protected override IIndicatorValue OnProcess(IIndicatorValue input)
		=> CalcZigZag(input, input.ToDecimal());

	/// <inheritdoc />
	protected ZigZagIndicatorValue CalcZigZag(IIndicatorValue input, decimal price)
	{
		if (input.IsFinal)
			_buffer.PushBack(price);

		if (!IsFormed)
			return new ZigZagIndicatorValue(this, input.Time);

		var lastExtremum = _lastExtremum ?? price;
		var isUpTrend = _isUpTrend ?? price >= _buffer[^2];

		var threshold = Delta;
		var changeTrend = false;

		if (isUpTrend)
		{
			if (lastExtremum < price)
				lastExtremum = price;
			else
				changeTrend = price <= (lastExtremum - threshold);
		}
		else
		{
			if (lastExtremum > price)
				lastExtremum = price;
			else
				changeTrend = price >= (lastExtremum + threshold);
		}

		if (changeTrend)
		{
			try
			{
				return new ZigZagIndicatorValue(this, lastExtremum, _shift, input.Time, isUpTrend);
			}
			finally
			{
				if (input.IsFinal)
				{
					_isUpTrend = !isUpTrend;
					_lastExtremum = price;
					_shift = 1;
				}
			}
		}
		else
		{
			if (input.IsFinal)
			{
				_lastExtremum = lastExtremum;
				_isUpTrend = isUpTrend;
				_shift++;
			}
		}

		return new ZigZagIndicatorValue(this, input.Time);
	}

	/// <inheritdoc />
	public override void Load(SettingsStorage storage)
	{
		base.Load(storage);

		Delta = storage.GetValue<decimal>(nameof(Delta));
	}

	/// <inheritdoc />
	public override void Save(SettingsStorage storage)
	{
		base.Save(storage);

		storage.SetValue(nameof(Delta), Delta);
	}

	/// <inheritdoc />
	public override string ToString() => base.ToString() + $" D={Delta}";
}
