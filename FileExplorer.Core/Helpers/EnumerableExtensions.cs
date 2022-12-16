namespace FileExplorer.Core.Helpers;

public static class EnumerableExtensions
{
	public static T[] ToArray<T>(this IEnumerable<T> source, out int length)
	{
		if (source is ICollection<T> ic)
		{
			var count = ic.Count;
			if (count != 0)
			{
				// Allocate an array of the desired size, then copy the elements into it. Note that this has the same
				// issue regarding concurrency as other existing collections like List<T>. If the collection size
				// concurrently changes between the array allocation and the CopyTo, we could end up either getting an
				// exception from overrunning the array (if the size went up) or we could end up not filling as many
				// items as 'count' suggests (if the size went down).  This is only an issue for concurrent collections
				// that implement ICollection<T>, which as of .NET 4.6 is just ConcurrentDictionary<TKey, TValue>.
				var arr = new T[count];
				ic.CopyTo(arr, 0);
				length = count;
				return arr;
			}
		}
		else
		{
			using (var en = source.GetEnumerator())
			{
				if (en.MoveNext())
				{
					const int DefaultCapacity = 4;
					var arr = new T[DefaultCapacity];
					arr[0] = en.Current;
					var count = 1;

					while (en.MoveNext())
					{
						if (count == arr.Length)
						{
							// This is the same growth logic as in List<T>:
							// If the array is currently empty, we make it a default size.  Otherwise, we attempt to
							// double the size of the array.  Doubling will overflow once the size of the array reaches
							// 2^30, since doubling to 2^31 is 1 larger than Int32.MaxValue.  In that case, we instead
							// constrain the length to be Array.MaxLength (this overflow check works because of the
							// cast to uint).
							var newLength = count << 1;
							if ((uint)newLength > Array.MaxLength)
							{
								newLength = Array.MaxLength <= count ? count + 1 : Array.MaxLength;
							}

							Array.Resize(ref arr, newLength);
						}

						arr[count++] = en.Current;
					}

					length = count;
					return arr;
				}
			}
		}

		length = 0;
		return Array.Empty<T>();
	}
}