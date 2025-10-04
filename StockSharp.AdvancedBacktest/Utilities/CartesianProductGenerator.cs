namespace StockSharp.AdvancedBacktest.Utilities;

public static class CartesianProductGenerator
{
	public static List<List<T>> Generate<T>(List<List<T>> lists)
	{
		if (lists == null || lists.Count == 0)
			return [];

		if (lists.Any(list => list == null || list.Count == 0))
			return [];

		List<List<T>> result = [];
		var indices = new int[lists.Count];

		while (true)
		{
			var current = new List<T>();
			for (var i = 0; i < lists.Count; i++)
				current.Add(lists[i][indices[i]]);

			result.Add(current);

			var index = lists.Count - 1;
			while (index >= 0 && indices[index] == lists[index].Count - 1)
				index--;

			if (index < 0)
				break;

			indices[index]++;
			for (var i = index + 1; i < lists.Count; i++)
				indices[i] = 0;
		}

		return result;
	}
}
