using System;
using Avalonia.Animation;
using Avalonia.Media;
using System.Reactive.Linq;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace FileExplorerCore.Helpers;

public class SolidColorBrushTransition : Transition<IBrush>
{
	public override IObservable<IBrush> DoTransition(IObservable<double> progress, IBrush oldValue, IBrush newValue)
	{
		// This strange behavior occurs on every application shutdown with alternating null values for a few calls.
		if (oldValue is null || newValue is null)
			return progress.Select(p => new SolidColorBrush(default(Color)));

		if (oldValue is not ISolidColorBrush oldBrush)
			throw new ArgumentException("Only instances of ISolidColorBrush are supported", nameof(oldValue));
		if (newValue is not ISolidColorBrush newBrush)
			throw new ArgumentException("Only instances of ISolidColorBrush are supported", nameof(newValue));

		var oldColor = oldBrush.Color;
		var newColor = newBrush.Color;

		return progress.Select(p =>
		{
			var e = Easing.Ease(p);

			var eVector = Vector128.Create((float)e);
			var halfVector = Vector128.Create(0.5f);

			var oldVector = Vector128.Create((float)oldColor.A, (float)oldColor.R, (float)oldColor.B, (float)oldColor.G);
			var newVector = Vector128.Create((float)newColor.A, (float)newColor.R, (float)newColor.B, (float)newColor.G);

			if (Fma.IsSupported)
			{
				var vector = Sse.Add(Fma.MultiplyAdd(halfVector, eVector, Sse.Subtract(newVector, oldVector)), oldVector);

				var vectorInt = Fma.ConvertToVector128Int32(vector);

				return new SolidColorBrush(new Color(
					(byte)vectorInt.GetElement(0), 
					(byte)vectorInt.GetElement(1),
					(byte)vectorInt.GetElement(2),
					(byte)vectorInt.GetElement(3)));
			}

			var A = (byte)(Math.FusedMultiplyAdd(e, newColor.A - oldColor.A, 0.5) + oldColor.A);
			var R = (byte)(Math.FusedMultiplyAdd(e, newColor.R - oldColor.R, 0.5) + oldColor.R);
			var G = (byte)(Math.FusedMultiplyAdd(e, newColor.G - oldColor.G, 0.5) + oldColor.G);
			var B = (byte)(Math.FusedMultiplyAdd(e, newColor.B - oldColor.B, 0.5) + oldColor.B);

			return new SolidColorBrush(new Color(A, R, G, B));
		});
	}
}