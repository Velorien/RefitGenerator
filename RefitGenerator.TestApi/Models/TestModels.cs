using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RefitGenerator.TestApi.Models
{
    public record NumericTestModel(int Integer, uint UnsignedInteger, long Long, ulong UnsignedLong,
                                   double Double, float Float, decimal Decimal,
                                   short Short, byte Byte);
}
