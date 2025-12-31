# Visual Guide: Suffix Array & LCP Array Construction and Searching

This document provides step-by-step visual representations of how the algorithms work in the DataSpecter codebase.

---

## Part 1: Suffix Array Construction (Doubling Algorithm)

### Input Data

Let's use a simple byte sequence for visualization:

```
Text: "banana"
Bytes: [98, 97, 110, 97, 110, 97]  (ASCII values)
Length: 6
```

---

### Iteration 0: Initialization

**Step 1.1: Create Initial Suffix Array and Rank Array**

```csharp
Parallel.For(0, n, i =>
{
    sa[i] = i;
    rank[i] = data[i];
});
```

**Visual State:**

```
Index (i):      0    1    2    3    4    5
Data:          'b'  'a'  'n'  'a'  'n'  'a'
Data (byte):    98   97  110   97  110   97

SA[i]:          0    1    2    3    4    5  (all suffixes by position)
Rank[i]:       98   97  110   97  110   97  (ranked by first byte)
```

**Suffixes at each position:**

```
SA[0] = 0 → "banana"
SA[1] = 1 → "anana"
SA[2] = 2 → "nana"
SA[3] = 3 → "ana"
SA[4] = 4 → "na"
SA[5] = 5 → "a"
```

---

### Iteration 1: Sort by First 2 Characters (k=1)

**Step 1.1: Create Pairs (first, second, index)**

```csharp
int k = 1;
Parallel.For(0, n, i =>
{
    int first = rank[sa[i]];
    int second = (sa[i] + k < n) ? rank[sa[i] + k] : -1;
    pairs[i] = (first, second, sa[i]);
});
```

**Visual Pair Creation:**

```
i=0: SA[0]=0, first=rank[0]=98 ('b'), second=rank[0+1]=rank[1]=97 ('a')
     → pair = (98, 97, 0)

i=1: SA[1]=1, first=rank[1]=97 ('a'), second=rank[1+1]=rank[2]=110 ('n')
     → pair = (97, 110, 1)

i=2: SA[2]=2, first=rank[2]=110 ('n'), second=rank[2+1]=rank[3]=97 ('a')
     → pair = (110, 97, 2)

i=3: SA[3]=3, first=rank[3]=97 ('a'), second=rank[3+1]=rank[4]=110 ('n')
     → pair = (97, 110, 3)

i=4: SA[4]=4, first=rank[4]=110 ('n'), second=rank[4+1]=rank[5]=97 ('a')
     → pair = (110, 97, 4)

i=5: SA[5]=5, first=rank[5]=97 ('a'), second=rank[5+1]=-1 (beyond end)
     → pair = (97, -1, 5)
```

**Pairs Array (before sorting):**

```
Index:  0           1           2           3           4           5
Pairs:  (98,97,0)   (97,110,1)  (110,97,2)  (97,110,3)  (110,97,4)  (97,-1,5)
        "ba"        "an"        "na"        "an"        "na"        "a"
```

**Step 1.2: Sort Pairs**

```csharp
Array.Sort(pairs, (a, b) =>
{
    if (a.first != b.first)
        return a.first.CompareTo(b.first);
    return a.second.CompareTo(b.second);
});
```

**After Sorting (lexicographically by 2-char prefix):**

```
Index:  0           1           2           3           4           5
Pairs:  (97,-1,5)   (97,110,1)  (97,110,3)  (98,97,0)   (110,97,2)  (110,97,4)
        "a"         "an"        "an"        "ba"        "na"        "na"
        ↑           ↑           ↑           ↑           ↑           ↑
        pos 5       pos 1       pos 3       pos 0       pos 2       pos 4
```

**Step 1.3: Extract Sorted Indices**

```csharp
Parallel.For(0, n, i =>
{
    sa[i] = pairs[i].index;
});
```

**New SA:**

```
SA[0] = 5  → "a"
SA[1] = 1  → "anana"
SA[2] = 3  → "ana"
SA[3] = 0  → "banana"
SA[4] = 2  → "nana"
SA[5] = 4  → "na"
```

**Step 1.4: Re-rank Based on Sorted Order**

```csharp
tempRank[sa[0]] = 0;
for (int i = 1; i < n; i++)
{
    bool same = (pairs[i].first == pairs[i-1].first) &&
                (pairs[i].second == pairs[i-1].second);
    tempRank[sa[i]] = same ? tempRank[sa[i-1]] : tempRank[sa[i-1]] + 1;
}
Array.Copy(tempRank, rank, n);
```

**Re-ranking Process:**

```
i=0: tempRank[SA[0]=5] = 0                    (first element)
i=1: Compare (97,110) vs (97,-1) → DIFFERENT  → tempRank[SA[1]=1] = 0+1 = 1
i=2: Compare (97,110) vs (97,110) → SAME      → tempRank[SA[2]=3] = 1 (keep same rank)
i=3: Compare (98,97) vs (97,110) → DIFFERENT  → tempRank[SA[3]=0] = 1+1 = 2
i=4: Compare (110,97) vs (98,97) → DIFFERENT  → tempRank[SA[4]=2] = 2+1 = 3
i=5: Compare (110,97) vs (110,97) → SAME      → tempRank[SA[5]=4] = 3 (keep same rank)
```

**New Rank Array:**

```
Index:       0    1    2    3    4    5
New Rank:    2    1    3    1    3    0
             ↑    ↑    ↑    ↑    ↑    ↑
             "b"  "an" "na" "an" "na" "a"
```

**State After k=1:**

```
SA:    [5, 1, 3, 0, 2, 4]
Rank:  [2, 1, 3, 1, 3, 0]
```

---

### Iteration 2: Sort by First 4 Characters (k=2)

**Step 2.1: Create Pairs**

```
i=0: SA[0]=5, first=rank[5]=0, second=rank[5+2]=-1 (out of bounds)
     → pair = (0, -1, 5)  ["a"]

i=1: SA[1]=1, first=rank[1]=1, second=rank[1+2]=rank[3]=1
     → pair = (1, 1, 1)  ["anan"]

i=2: SA[2]=3, first=rank[3]=1, second=rank[3+2]=rank[5]=0
     → pair = (1, 0, 3)  ["ana"]

i=3: SA[3]=0, first=rank[0]=2, second=rank[0+2]=rank[2]=3
     → pair = (2, 3, 0)  ["bana"]

i=4: SA[4]=2, first=rank[2]=3, second=rank[2+2]=rank[4]=3
     → pair = (3, 3, 2)  ["nana"]

i=5: SA[5]=4, first=rank[4]=3, second=rank[4+2]=-1 (out of bounds)
     → pair = (3, -1, 4)  ["na"]
```

**Step 2.2: Sort Pairs**

```
Before: [(0,-1,5), (1,1,1), (1,0,3), (2,3,0), (3,3,2), (3,-1,4)]
After:  [(0,-1,5), (1,0,3), (1,1,1), (2,3,0), (3,-1,4), (3,3,2)]
         "a"       "ana"    "anan"   "bana"   "na"     "nana"
```

**New SA:**

```
SA = [5, 3, 1, 0, 4, 2]
```

**Step 2.3: Re-rank**

```
i=0: tempRank[5] = 0
i=1: (1,0) vs (0,-1) → DIFFERENT → tempRank[3] = 1
i=2: (1,1) vs (1,0) → DIFFERENT  → tempRank[1] = 2
i=3: (2,3) vs (1,1) → DIFFERENT  → tempRank[0] = 3
i=4: (3,-1) vs (2,3) → DIFFERENT → tempRank[4] = 4
i=5: (3,3) vs (3,-1) → DIFFERENT → tempRank[2] = 5
```

**New Rank Array:**

```
Index:     0   1   2   3   4   5
Rank:      3   2   5   1   4   0
```

**Check termination:** rank[sa[n-1]] = rank[sa[5]] = rank[2] = 5 = n-1 ✓

**All ranks are unique! Construction complete.**

---

### Final Suffix Array Result

```
Position in SA:  0   1   2   3   4   5
SA (offset):     5   3   1   0   4   2
Suffix:         "a" "ana" "anana" "banana" "na" "nana"

Lexicographic Order (verified):
"a" < "ana" < "anana" < "banana" < "na" < "nana" ✓
```

---

## Part 2: LCP Array Construction (Kasai's Algorithm)

### Input

```
data:  [98, 97, 110, 97, 110, 97]  ("banana")
SA:    [5, 3, 1, 0, 4, 2]
rank:  [3, 2, 5, 1, 4, 0]  (inverse of SA)
```

**Verify rank array (inverse of SA):**

```
SA[rank[0]] = SA[3] = 0 ✓
SA[rank[1]] = SA[2] = 1 ✓
SA[rank[2]] = SA[5] = 2 ✓
...
```

---

### Algorithm Execution

**Initialize:**

```csharp
int[] lcp = new int[n];
int h = 0;
LCP[0] = 0;  // By convention
```

**State:**

```
LCP: [0, ?, ?, ?, ?, ?]
h = 0
```

---

### i = 0 (Process text position 0: "banana")

```csharp
if (rank[i] > 0)  // rank[0] = 3 > 0 ✓
{
    int j = sa[rank[i] - 1];  // j = sa[3-1] = sa[2] = 1
```

**Compare suffixes:**

- Suffix at position i=0: "**ba**nana"
- Suffix at position j=1: "**a**nana"

```csharp
while (i + h < n && j + h < n && data[i + h] == data[j + h])
    h++;
```

**Matching process:**

```
h=0: data[0+0]='b' vs data[1+0]='a' → 'b'≠'a' → STOP
h stays 0
```

```csharp
lcp[rank[i]] = h;  // lcp[3] = 0
if (h > 0) h--;    // h = max(0, 0-1) = 0
```

**State:**

```
LCP: [0, ?, ?, 0, ?, ?]
h = 0
```

---

### i = 1 (Process text position 1: "anana")

```csharp
rank[1] = 2 > 0 ✓
j = sa[rank[1] - 1] = sa[2-1] = sa[1] = 3
```

**Compare suffixes:**

- Suffix at position i=1: "**ana**na"
- Suffix at position j=3: "**ana**"

```csharp
while (i + h < n && j + h < n && data[i + h] == data[j + h])
    h++;
```

**Matching process:**

```
h=0: data[1+0]='a' vs data[3+0]='a' → 'a'='a' ✓ → h=1
h=1: data[1+1]='n' vs data[3+1]='n' → 'n'='n' ✓ → h=2
h=2: data[1+2]='a' vs data[3+2]='a' → 'a'='a' ✓ → h=3
h=3: data[1+3]='n' vs data[3+3] → j+h=6 >= n → STOP
```

```csharp
lcp[rank[1]] = h;  // lcp[2] = 3
if (h > 0) h--;    // h = 3-1 = 2
```

**State:**

```
LCP: [0, ?, 3, 0, ?, ?]
h = 2
```

---

### i = 2 (Process text position 2: "nana")

```csharp
rank[2] = 5 > 0 ✓
j = sa[rank[2] - 1] = sa[5-1] = sa[4] = 4
```

**Compare suffixes:**

- Suffix at position i=2: "**nana**"
- Suffix at position j=4: "**na**"

**Key insight:** We start from h=2 (carried from previous iteration)

```csharp
while (i + h < n && j + h < n && data[i + h] == data[j + h])
    h++;
```

**Matching process:**

```
h=2: data[2+2]='n' vs data[4+2] → j+h=6 >= n → STOP
(We already know first 2 chars match from previous knowledge)
```

```csharp
lcp[rank[2]] = h;  // lcp[5] = 2
if (h > 0) h--;    // h = 2-1 = 1
```

**State:**

```
LCP: [0, ?, 3, 0, ?, 2]
h = 1
```

---

### i = 3 (Process text position 3: "ana")

```csharp
rank[3] = 1 > 0 ✓
j = sa[rank[3] - 1] = sa[1-1] = sa[0] = 5
```

**Compare suffixes:**

- Suffix at position i=3: "**a**na"
- Suffix at position j=5: "**a**"

```csharp
while (i + h < n && j + h < n && data[i + h] == data[j + h])
    h++;
```

**Matching process:**

```
h=1: data[3+1]='n' vs data[5+1] → j+h=6 >= n → STOP
(First char 'a' already matched from h=1 carryover)
```

```csharp
lcp[rank[3]] = h;  // lcp[1] = 1
if (h > 0) h--;    // h = 1-1 = 0
```

**State:**

```
LCP: [0, 1, 3, 0, ?, 2]
h = 0
```

---

### i = 4 (Process text position 4: "na")

```csharp
rank[4] = 4 > 0 ✓
j = sa[rank[4] - 1] = sa[4-1] = sa[3] = 0
```

**Compare suffixes:**

- Suffix at position i=4: "na"
- Suffix at position j=0: "banana"

```csharp
while (i + h < n && j + h < n && data[i + h] == data[j + h])
    h++;
```

**Matching process:**

```
h=0: data[4+0]='n' vs data[0+0]='b' → 'n'≠'b' → STOP
```

```csharp
lcp[rank[4]] = h;  // lcp[4] = 0
if (h > 0) h--;    // h = max(0, 0-1) = 0
```

**State:**

```
LCP: [0, 1, 3, 0, 0, 2]
h = 0
```

---

### i = 5 (Process text position 5: "a")

```csharp
rank[5] = 0 > 0 → FALSE
// Skip (lexicographically first suffix has no predecessor)
```

**Final State:**

```
LCP: [0, 1, 3, 0, 0, 2]
```

---

### LCP Array Complete - Interpretation

```
SA Index:  0     1     2       3         4     5
SA:        5     3     1       0         4     2
Suffix:   "a"  "ana" "anana" "banana"  "na"  "nana"
LCP:       0     1     3       0         0     2
           ↑     ↑     ↑       ↑         ↑     ↑
         (no   "a"   "ana"   (none)   (none)  "na"
          prev)  ←─────→
                common
```

**What each LCP value means:**

- **LCP[0] = 0**: By convention (no previous suffix)
- **LCP[1] = 1**: "a" and "ana" share **"a"** (1 char)
- **LCP[2] = 3**: "ana" and "anana" share **"ana"** (3 chars)
- **LCP[3] = 0**: "anana" and "banana" share nothing
- **LCP[4] = 0**: "banana" and "na" share nothing
- **LCP[5] = 2**: "na" and "nana" share **"na"** (2 chars)

**Maximum LCP = 3** → Longest Repeated Substring = "ana" (3 bytes)

---

## Part 3: Pattern Searching with Binary Search

### Scenario: Search for Pattern "na"

```
Pattern: "na" = [110, 97]
Data: "banana" = [98, 97, 110, 97, 110, 97]
SA: [5, 3, 1, 0, 4, 2]
```

**Our goal:** Find all positions where "na" appears in "banana"

**Expected result:** Positions 2 and 4 ("ba**na**na")

---

### Phase 1: Binary Search for Lower Bound

**Find the first suffix that starts with "na" (or lexicographically ≥ "na")**

```csharp
int l = 0, r = n - 1;
int start = -1;

while (l <= r)
{
    int mid = l + (r - l) / 2;
    int suffixStart = sa[mid];
    int cmp = Compare(dataSource, suffixStart, pattern);

    if (cmp >= 0)  // suffix >= pattern
    {
        if (cmp == 0) start = mid;
        r = mid - 1;
    }
    else
    {
        l = mid + 1;
    }
}
```

---

#### Lower Bound: Iteration 1

```
l = 0, r = 5
mid = 0 + (5-0)/2 = 2

SA[mid=2] = 1 → Suffix: "anana"
Compare("anana", "na"):
  'a' vs 'n': 'a' < 'n' → cmp = -1

cmp < 0 → l = mid + 1 = 3
```

**State:**

```
     l              mid          r
     ↓               ↓           ↓
SA:  5     3     1   0   4   2
    "a"  "ana" "anana" "banana" "na" "nana"
                                 ↑
                             (new l)
```

---

#### Lower Bound: Iteration 2

```
l = 3, r = 5
mid = 3 + (5-3)/2 = 4

SA[mid=4] = 4 → Suffix: "na"
Compare("na", "na"):
  'n' vs 'n': equal
  'a' vs 'a': equal
  → cmp = 0 (MATCH!)

cmp == 0 → start = 4
cmp >= 0 → r = mid - 1 = 3
```

**State:**

```
     l=r            mid
     ↓               ↓
SA:  5     3     1   0   4   2
    "a"  "ana" "anana" "banana" "na" "nana"
                              ✓ start=4
```

---

#### Lower Bound: Iteration 3

```
l = 3, r = 3
mid = 3 + (3-3)/2 = 3

SA[mid=3] = 0 → Suffix: "banana"
Compare("banana", "na"):
  'b' vs 'n': 'b' < 'n' → cmp = -1

cmp < 0 → l = mid + 1 = 4
```

**State:**

```
                              l>r
                               ↓
SA:  5     3     1   0   4   2
    "a"  "ana" "anana" "banana" "na" "nana"
                              ✓ start=4
```

**Loop ends (l > r)**

**Lower bound found: start = 4**

---

### Phase 2: Binary Search for Upper Bound

**Find the last suffix that starts with "na"**

```csharp
l = start;  // l = 4
r = n - 1;  // r = 5
int end = -1;

while (l <= r)
{
    int mid = l + (r - l) / 2;
    int suffixStart = sa[mid];
    int cmp = Compare(dataSource, suffixStart, pattern);

    if (cmp <= 0)  // suffix <= pattern
    {
        if (cmp == 0) end = mid;
        l = mid + 1;
    }
    else
    {
        r = mid - 1;
    }
}
```

---

#### Upper Bound: Iteration 1

```
l = 4, r = 5
mid = 4 + (5-4)/2 = 4

SA[mid=4] = 4 → Suffix: "na"
Compare("na", "na"):
  → cmp = 0 (MATCH!)

cmp == 0 → end = 4
cmp <= 0 → l = mid + 1 = 5
```

**State:**

```
                                  l   r
                                  ↓   ↓
SA:  5     3     1   0   4   2
    "a"  "ana" "anana" "banana" "na" "nana"
                              ✓      ?
                            end=4
```

---

#### Upper Bound: Iteration 2

```
l = 5, r = 5
mid = 5 + (5-5)/2 = 5

SA[mid=5] = 2 → Suffix: "nana"
Compare("nana", "na"):
  'n' vs 'n': equal
  'a' vs 'a': equal
  Check if we read all pattern bytes: YES
  → cmp = 0 (MATCH!)

cmp == 0 → end = 5
cmp <= 0 → l = mid + 1 = 6
```

**State:**

```
                                     l>r
                                      ↓
SA:  5     3     1   0   4   2
    "a"  "ana" "anana" "banana" "na" "nana"
                              ✓      ✓
                            start  end=5
```

**Loop ends (l > r)**

**Upper bound found: end = 5**

---

### Phase 3: Extract Results

```csharp
int count = end - start + 1;  // 5 - 4 + 1 = 2 matches
long[] offsets = new long[count];
for (int i = 0; i < count; i++)
{
    offsets[i] = sa[start + i];
}
```

**Extraction:**

```
i=0: offsets[0] = sa[4] = 4  → Position 4 in text
i=1: offsets[1] = sa[5] = 2  → Position 2 in text
```

**Sort offsets for display:**

```csharp
Array.Sort(offsets);  // [2, 4]
```

---

### Final Result Verification

```
Text:    b  a  n  a  n  a
Index:   0  1  2  3  4  5
                ↑     ↑
              "na"   "na"
            pos 2  pos 4
```

**✓ Found 2 occurrences at positions [2, 4]**

---

## Visual Summary: Complete Search Flow

```
┌──────────────────────────────────────────────────────────┐
│ Input: Pattern "na" [110, 97]                            │
└──────────────────────────────────────────────────────────┘
                        ↓
┌──────────────────────────────────────────────────────────┐
│ Suffix Array (pre-built):                                │
│   Index:  0     1     2       3         4     5          │
│   SA:     5     3     1       0         4     2          │
│   Text:  "a"  "ana" "anana" "banana"  "na"  "nana"       │
└──────────────────────────────────────────────────────────┘
                        ↓
┌──────────────────────────────────────────────────────────┐
│ Binary Search for Lower Bound:                           │
│   Compare suffixes until cmp >= 0 and cmp == 0           │
│   Result: start = 4                                      │
└──────────────────────────────────────────────────────────┘
                        ↓
┌──────────────────────────────────────────────────────────┐
│ Binary Search for Upper Bound:                           │
│   Compare suffixes until cmp <= 0 and cmp == 0           │
│   Result: end = 5                                        │
└──────────────────────────────────────────────────────────┘
                        ↓
┌──────────────────────────────────────────────────────────┐
│ Extract Range [start, end]:                              │
│   SA[4] = 4 → Text position 4                            │
│   SA[5] = 2 → Text position 2                            │
└──────────────────────────────────────────────────────────┘
                        ↓
┌──────────────────────────────────────────────────────────┐
│ Sort and Return: [2, 4]                                  │
└──────────────────────────────────────────────────────────┘
```

---

## Part 4: Compare Function Deep Dive

### How Compare Works

```csharp
private int Compare(BinaryDataSource dataSource, int textOffset, byte[] pattern)
{
    long len = dataSource.Length;
    int limit = pattern.Length;

    if (textOffset >= len) return -1;

    int availableBytes = (int)Math.Min(limit, len - textOffset);
    byte[] buffer = new byte[availableBytes];
    int bytesRead = dataSource.ReadRange(textOffset, buffer, 0, availableBytes);

    for (int i = 0; i < bytesRead; i++)
    {
        if (buffer[i] < pattern[i]) return -1;  // text < pattern
        if (buffer[i] > pattern[i]) return 1;   // text > pattern
    }

    if (bytesRead < limit) return -1;  // text is shorter
    return 0;  // exact match
}
```

### Example Comparisons

**Compare("anana", "na"):**

```
textOffset = 1, pattern = "na"

buffer = ['a', 'n', 'a', 'n', 'a']  (read from position 1)
pattern = ['n', 'a']

i=0: buffer[0]='a'(97) vs pattern[0]='n'(110)
     97 < 110 → return -1 (text < pattern)
```

**Compare("na", "na"):**

```
textOffset = 4, pattern = "na"

buffer = ['n', 'a']
pattern = ['n', 'a']

i=0: buffer[0]='n'(110) vs pattern[0]='n'(110) → equal, continue
i=1: buffer[1]='a'(97) vs pattern[1]='a'(97) → equal, continue
Loop ends, bytesRead(2) == limit(2) → return 0 (exact match)
```

**Compare("nana", "na"):**

```
textOffset = 2, pattern = "na"

buffer = ['n', 'a']  (only read first 2 bytes, limit=2)
pattern = ['n', 'a']

i=0: 'n' == 'n' → continue
i=1: 'a' == 'a' → continue
Loop ends, bytesRead(2) == limit(2) → return 0 (exact match)
```

---

## Complexity Analysis Visualization

### Time Complexity per Operation

```
┌─────────────────────────────────────────────────────────────┐
│ Operation                  │ Comparisons                    │
├────────────────────────────┼────────────────────────────────┤
│ Suffix Array Construction  │ O(n log² n)                    │
│   - Iterations: log n      │                                │
│   - Each sort: O(n log n)  │                                │
├────────────────────────────┼────────────────────────────────┤
│ LCP Array Construction     │ O(n)                           │
│   - h increments: ≤ n      │                                │
│   - h decrements: ≤ n      │                                │
│   - Total: 2n = O(n)       │                                │
├────────────────────────────┼────────────────────────────────┤
│ Pattern Search             │ O(M log N)                     │
│   - Binary searches: 2     │                                │
│   - Each: log N steps      │                                │
│   - Compare per step: M    │                                │
│   - Total: 2 × M × log N   │                                │
└─────────────────────────────────────────────────────────────┘

Where:
  n = file size (bytes)
  M = pattern length (bytes)
  N = file size (= n)
```

### Example with 100MB File

```
File size: n = 100,000,000 bytes
Pattern: M = 10 bytes

┌──────────────────────────────────────────────────────────┐
│ Suffix Array Build:                                      │
│   O(n log² n) = 100M × log²(100M)                        │
│                ≈ 100M × (27)² ≈ 72 billion ops           │
│   With parallelization: ~10-30 seconds                   │
└──────────────────────────────────────────────────────────┘
                        ↓
┌──────────────────────────────────────────────────────────┐
│ LCP Array Build:                                         │
│   O(n) = 100M operations                                 │
│   ~1-5 seconds                                           │
└──────────────────────────────────────────────────────────┘
                        ↓
┌──────────────────────────────────────────────────────────┐
│ Pattern Search (once SA is built):                      │
│   O(M log N) = 10 × log₂(100M)                          │
│              = 10 × 27 = 270 comparisons                 │
│   ~0.001 seconds (instant!)                              │
└──────────────────────────────────────────────────────────┘
```

### Comparison: Naive vs Suffix Array

```
Naive Search (without Suffix Array):
────────────────────────────────────
For each position i in text:
  Compare pattern with text[i:i+M]

Worst case: O(N × M) = 100M × 10 = 1 billion comparisons
Time: ~1-2 seconds per search


Suffix Array Search:
────────────────────
Binary search on sorted suffixes

Comparisons: O(M log N) = 10 × 27 = 270 comparisons
Time: ~0.001 seconds per search

Speedup: ~1000x - 2000x faster! 🚀
```

---

## Memory Layout Visualization

### Arrays in Memory (100MB file)

```
File: 100,000,000 bytes

┌─────────────────────────────────────────────┐
│ Original Data:           100,000,000 bytes  │
│ (read-only, on disk)                        │
└─────────────────────────────────────────────┘

┌─────────────────────────────────────────────┐
│ Suffix Array (SA):       400,000,000 bytes  │
│ - 100M integers × 4 bytes                   │
│ - Stores: [0, 1, 2, ..., 99999999]         │
│ - Sorted by suffix order                    │
└─────────────────────────────────────────────┘

┌─────────────────────────────────────────────┐
│ LCP Array:               400,000,000 bytes  │
│ - 100M integers × 4 bytes                   │
│ - Stores: [0, 5, 12, 3, ...]               │
└─────────────────────────────────────────────┘

┌─────────────────────────────────────────────┐
│ Rank Array (temp):       400,000,000 bytes  │
│ - Only during construction                  │
│ - Released after LCP built                  │
└─────────────────────────────────────────────┘

Total Peak Memory: ~1.2 GB (during construction)
Total Memory After: ~800 MB (SA + LCP only)
```

---

## Cache Optimization

### Cache Structure

```csharp
private readonly ConcurrentDictionary<string,
    (int[] sa, int[] lcp, long size, DateTime modified)> _cache = new();
```

### Visual Flow with Cache

```
┌────────────────┐
│ User opens     │
│ file "test.bin"│
└───────┬────────┘
        │
        ↓
┌──────────────────────────────────┐
│ Check cache:                     │
│   Key = hash(test.bin) + size    │
│   Found? NO                      │
└───────┬──────────────────────────┘
        │
        ↓
┌──────────────────────────────────┐
│ Build SA + LCP:                  │
│   [████████████████] 100%        │
│   Time: 15 seconds               │
└───────┬──────────────────────────┘
        │
        ↓
┌──────────────────────────────────┐
│ Store in cache:                  │
│   cache["test.bin"] = (sa, lcp)  │
└───────┬──────────────────────────┘
        │
        ↓
┌──────────────────────────────────┐
│ User searches: INSTANT ⚡         │
└──────────────────────────────────┘

Later...

┌────────────────┐
│ User opens     │
│ file "test.bin"│
└───────┬────────┘
        │
        ↓
┌──────────────────────────────────┐
│ Check cache:                     │
│   Key = hash(test.bin) + size    │
│   Found? YES ✓                   │
│   Load from cache: INSTANT! ⚡    │
└──────────────────────────────────┘
```

---

This visualization demonstrates the complete construction and search process used in DataSpecter's implementation.
