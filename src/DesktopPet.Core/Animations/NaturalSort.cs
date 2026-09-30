namespace DesktopPet.Animations;

public static class NaturalSort
{
    public static IReadOnlyList<string> Order(IEnumerable<string> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return values
            .Select((value, index) => (value, index))
            .OrderBy(entry => entry.value, NaturalComparer.Instance)
            .ThenBy(entry => entry.index)
            .Select(entry => entry.value)
            .ToArray();
    }

    private sealed class NaturalComparer : IComparer<string>
    {
        public static NaturalComparer Instance { get; } = new();

        public int Compare(string? left, string? right)
        {
            if (ReferenceEquals(left, right)) return 0;
            if (left is null) return -1;
            if (right is null) return 1;

            var leftIndex = 0;
            var rightIndex = 0;
            while (leftIndex < left.Length && rightIndex < right.Length)
            {
                var leftIsDigit = char.IsDigit(left[leftIndex]);
                var rightIsDigit = char.IsDigit(right[rightIndex]);
                if (leftIsDigit && rightIsDigit)
                {
                    var comparison = CompareDigitRuns(left, ref leftIndex, right, ref rightIndex);
                    if (comparison != 0) return comparison;
                    continue;
                }

                var leftCharacter = char.ToUpperInvariant(left[leftIndex]);
                var rightCharacter = char.ToUpperInvariant(right[rightIndex]);
                if (leftCharacter != rightCharacter)
                {
                    return leftCharacter.CompareTo(rightCharacter);
                }

                leftIndex++;
                rightIndex++;
            }

            var lengthComparison = left.Length.CompareTo(right.Length);
            return lengthComparison != 0 ? lengthComparison :
                StringComparer.Ordinal.Compare(left, right);
        }

        private static int CompareDigitRuns(string left, ref int leftIndex,
            string right, ref int rightIndex)
        {
            var leftStart = leftIndex;
            var rightStart = rightIndex;
            while (leftIndex < left.Length && char.IsDigit(left[leftIndex])) leftIndex++;
            while (rightIndex < right.Length && char.IsDigit(right[rightIndex])) rightIndex++;

            var leftFirstSignificant = leftStart;
            while (leftFirstSignificant < leftIndex && left[leftFirstSignificant] == '0')
            {
                leftFirstSignificant++;
            }

            var rightFirstSignificant = rightStart;
            while (rightFirstSignificant < rightIndex && right[rightFirstSignificant] == '0')
            {
                rightFirstSignificant++;
            }

            var leftSignificantLength = leftIndex - leftFirstSignificant;
            var rightSignificantLength = rightIndex - rightFirstSignificant;
            var comparison = leftSignificantLength.CompareTo(rightSignificantLength);
            if (comparison != 0) return comparison;

            comparison = string.Compare(left, leftFirstSignificant, right, rightFirstSignificant,
                leftSignificantLength, StringComparison.Ordinal);
            if (comparison != 0) return comparison;

            // Equal numeric values sort by the shorter spelling: 2 before 02.
            comparison = (leftIndex - leftStart).CompareTo(rightIndex - rightStart);
            return comparison;
        }
    }
}
