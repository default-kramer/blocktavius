using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Antipasta;

static class Util
{
	public static Internalized<T> Internalize<T>(this T value) => new Internalized<T>(value);
}
