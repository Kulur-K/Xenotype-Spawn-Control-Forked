// Copyright (c) 2022 bradson
// This Source Code Form is subject to the terms of the MIT license.
// If a copy of the license was not distributed with this file,
// You can obtain one at https://opensource.org/licenses/MIT/.

namespace XenotypeSpawnControl;

public static class ExtensionMethods
{
	public static TValue GetOrAdd<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TKey key)
		where TValue : new() where TKey : notnull
	{
		if (!dictionary.TryGetValue(key, out var value) || value is null)
			dictionary[key] = value = new();

		return value;
	}

	/// <summary>
	/// FORKED: generated xenotypes reuse a single CustomXenotype instance. Hand out copies, so pawns or the game's
	/// custom xenotype database never keep a reference to an object that gets overwritten on the next generation.
	/// </summary>
	public static CustomXenotype Copy(this CustomXenotype xenotype)
		=> new()
		{
			name = xenotype.name,
			genes = [..xenotype.genes],
			inheritable = xenotype.inheritable,
			iconDef = xenotype.iconDef
		};

	public static int Sum<TKey>(this List<TKey> list, Func<TKey, int> selector)
	{
		var sum = 0;
		for (var i = 0; i < list.Count; i++)
			sum += selector(list[i]);
		
		return sum;
	}

	public static float Sum<TKey>(this List<TKey> list, Func<TKey, float> selector)
	{
		var sum = 0f;
		for (var i = 0; i < list.Count; i++)
			sum += selector(list[i]);
		
		return sum;
	}
}