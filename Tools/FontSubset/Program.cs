// TrueType(glyf)フォントを、指定した文字だけに絞る(04-screens.md「フォントの作り方」。2026-10-04)。
// Google Fonts の「text= で字を指定する」取得は約 800 字までなので、全文字版を 1 回取得して手元で絞る。
//
//   dotnet run --project Tools/FontSubset -- <元.ttf> <文字の一覧.txt> <出力.ttf>
//
// 残す表: head hhea maxp hmtx loca glyf cmap name OS/2 post(版 3 = 字形名なし)と、ヒントの cvt fpgm prep gasp。
// 字詰め・字形の置き換え(GSUB/GPOS/kern)や縦書き(vhea/vmtx)は Unity の Text が使わないので落とす。
using System.Text;

if (args.Length != 3)
{
    Console.Error.WriteLine("使い方: FontSubset <元.ttf> <文字の一覧.txt> <出力.ttf>");
    return 1;
}
var src = File.ReadAllBytes(args[0]);
var wanted = File.ReadAllText(args[1], Encoding.UTF8).EnumerateRunes().Select(r => r.Value).Where(c => c >= 0x20).Distinct().OrderBy(c => c).ToList();

if (U32(src, 0) != 0x00010000) { Console.Error.WriteLine("TrueType(glyf)のフォントではない"); return 1; }
var tables = new Dictionary<string, (int off, int len)>();
for (int i = 0, n = U16(src, 4); i < n; i++)
{
    int r = 12 + i * 16;
    tables[Encoding.ASCII.GetString(src, r, 4)] = ((int)U32(src, r + 8), (int)U32(src, r + 12));
}
byte[] Table(string tag) { var (o, l) = tables[tag]; return src.AsSpan(o, l).ToArray(); }

// ---- 文字 → 字形番号(cmap の 3/10 形式 12、なければ 3/1 形式 4) ----
var cmap = Table("cmap");
var map = new Dictionary<int, int>();
int best = -1, bestFormat = 0;
for (int i = 0, n = U16(cmap, 2); i < n; i++)
{
    int pid = U16(cmap, 4 + i * 8), eid = U16(cmap, 6 + i * 8), off = (int)U32(cmap, 8 + i * 8), fmt = U16(cmap, off);
    if (pid == 3 && eid == 10 && fmt == 12) { best = off; bestFormat = 12; break; }
    if (pid == 3 && eid == 1 && fmt == 4 && bestFormat == 0) { best = off; bestFormat = 4; }
}
if (best < 0) { Console.Error.WriteLine("Windows Unicode の cmap が無い"); return 1; }
if (bestFormat == 12)
{
    for (uint k = 0, groups = U32(cmap, best + 12); k < groups; k++)
    {
        int g = best + 16 + (int)k * 12;
        uint start = U32(cmap, g), end = U32(cmap, g + 4), gid = U32(cmap, g + 8);
        for (uint c = start; c <= end; c++) map[(int)c] = (int)(gid + c - start);
    }
}
else
{
    int segX2 = U16(cmap, best + 6), ends = best + 14, starts = ends + segX2 + 2, deltas = starts + segX2, ranges = deltas + segX2;
    for (int s = 0; s < segX2 / 2; s++)
    {
        int end = U16(cmap, ends + s * 2), start = U16(cmap, starts + s * 2), delta = (short)U16(cmap, deltas + s * 2), ro = U16(cmap, ranges + s * 2);
        for (int c = start; c <= end && c != 0xFFFF; c++)
        {
            int g = ro == 0 ? (c + delta) & 0xFFFF : U16(cmap, ranges + s * 2 + ro + (c - start) * 2);
            if (g != 0 && ro != 0) g = (g + delta) & 0xFFFF;
            if (g != 0) map[c] = g;
        }
    }
}

// ---- 残す字形(.notdef + 指定の字 + 合成字形の部品) ----
var head = Table("head"); var loca = Table("loca"); var glyf = Table("glyf");
bool longLoca = U16(head, 50) == 1;
int numGlyphs = U16(Table("maxp"), 4);
int GlyphOff(int g) => longLoca ? (int)U32(loca, g * 4) : U16(loca, g * 2) * 2;
var keep = new SortedSet<int> { 0 };
var missing = new List<int>();
foreach (var c in wanted) { if (map.TryGetValue(c, out var g)) keep.Add(g); else missing.Add(c); }
var stack = new Stack<int>(keep);
while (stack.Count > 0)
{
    int g = stack.Pop();
    foreach (var comp in Components(g)) if (keep.Add(comp.gid)) stack.Push(comp.gid);
}
var newId = new Dictionary<int, int>();
foreach (var g in keep) newId[g] = newId.Count;

// ---- glyf / loca(長い形式) ----
var newGlyf = new MemoryStream();
var newLoca = new List<uint>();
foreach (var g in keep)
{
    newLoca.Add((uint)newGlyf.Length);
    int o = GlyphOff(g), len = GlyphOff(g + 1) - o;
    if (len <= 0) continue;
    var bytes = glyf.AsSpan(o, len).ToArray();
    foreach (var comp in Components(g)) W16(bytes, comp.pos, newId[comp.gid]);   // 合成字形の部品の番号を付け替える
    newGlyf.Write(bytes);
    while (newGlyf.Length % 4 != 0) newGlyf.WriteByte(0);
}
newLoca.Add((uint)newGlyf.Length);
var locaBytes = new byte[newLoca.Count * 4];
for (int i = 0; i < newLoca.Count; i++) W32(locaBytes, i * 4, newLoca[i]);

// ---- hmtx(全部の字形に送り幅を持たせる) ----
var hhea = Table("hhea"); var hmtx = Table("hmtx");
int numHMetrics = U16(hhea, 34);
var hmtxOut = new byte[keep.Count * 4];
int idx = 0;
foreach (var g in keep)
{
    int adv = g < numHMetrics ? U16(hmtx, g * 4) : U16(hmtx, (numHMetrics - 1) * 4);
    int lsb = g < numHMetrics ? U16(hmtx, g * 4 + 2) : U16(hmtx, numHMetrics * 4 + (g - numHMetrics) * 2);
    W16(hmtxOut, idx * 4, adv); W16(hmtxOut, idx * 4 + 2, lsb); idx++;
}
W16(hhea, 34, keep.Count);
var maxp = Table("maxp"); W16(maxp, 4, keep.Count);
W16(head, 50, 1); W32(head, 8, 0);   // indexToLocFormat = 長い形式、checkSumAdjustment は最後に入れる
var post = Table("post").AsSpan(0, 32).ToArray(); W32(post, 0, 0x00030000);

// ---- cmap(3/1 形式 4 と 3/10 形式 12) ----
var pairs = wanted.Where(map.ContainsKey).Select(c => (c, g: newId[map[c]])).ToList();
var bmp = pairs.Where(p => p.c <= 0xFFFF).ToList();
var f4 = new MemoryStream();
{
    int segCount = bmp.Count + 1;
    int searchRange = 2 * (1 << (int)Math.Floor(Math.Log2(segCount)));
    void P16(MemoryStream m, int v) { m.WriteByte((byte)(v >> 8)); m.WriteByte((byte)v); }
    P16(f4, 4); P16(f4, 16 + segCount * 8); P16(f4, 0); P16(f4, segCount * 2);
    P16(f4, searchRange); P16(f4, (int)Math.Log2(searchRange / 2)); P16(f4, segCount * 2 - searchRange);
    foreach (var p in bmp) P16(f4, p.c); P16(f4, 0xFFFF);
    P16(f4, 0);
    foreach (var p in bmp) P16(f4, p.c); P16(f4, 0xFFFF);
    foreach (var p in bmp) P16(f4, (p.g - p.c) & 0xFFFF); P16(f4, 1);
    foreach (var _ in bmp) P16(f4, 0); P16(f4, 0);
}
var f12 = new MemoryStream();
{
    void P32(MemoryStream m, uint v) { m.WriteByte((byte)(v >> 24)); m.WriteByte((byte)(v >> 16)); m.WriteByte((byte)(v >> 8)); m.WriteByte((byte)v); }
    f12.WriteByte(0); f12.WriteByte(12); f12.WriteByte(0); f12.WriteByte(0);
    P32(f12, (uint)(16 + pairs.Count * 12)); P32(f12, 0); P32(f12, (uint)pairs.Count);
    foreach (var p in pairs) { P32(f12, (uint)p.c); P32(f12, (uint)p.c); P32(f12, (uint)p.g); }
}
var cmapOut = new byte[4 + 2 * 8 + f4.Length + f12.Length];
W16(cmapOut, 2, 2);
W16(cmapOut, 4, 3); W16(cmapOut, 6, 1); W32(cmapOut, 8, 20);
W16(cmapOut, 12, 3); W16(cmapOut, 14, 10); W32(cmapOut, 16, (uint)(20 + f4.Length));
f4.ToArray().CopyTo(cmapOut, 20); f12.ToArray().CopyTo(cmapOut, 20 + (int)f4.Length);

// ---- 書き出し ----
var outTables = new SortedDictionary<string, byte[]>(StringComparer.Ordinal)
{
    ["head"] = head, ["hhea"] = hhea, ["maxp"] = maxp, ["hmtx"] = hmtxOut, ["loca"] = locaBytes,
    ["glyf"] = newGlyf.ToArray(), ["cmap"] = cmapOut, ["post"] = post,
};
foreach (var tag in new[] { "name", "OS/2", "cvt ", "fpgm", "prep", "gasp" })
    if (tables.ContainsKey(tag)) outTables[tag] = Table(tag);
int nt = outTables.Count, sr = 16 * (1 << (int)Math.Floor(Math.Log2(nt)));
var outBuf = new MemoryStream();
var dir = new byte[12 + nt * 16];
W32(dir, 0, 0x00010000); W16(dir, 4, nt); W16(dir, 6, sr); W16(dir, 8, (int)Math.Log2(sr / 16)); W16(dir, 10, nt * 16 - sr);
outBuf.Write(dir);
int ti = 0, headPos = 0;
foreach (var (tag, data) in outTables)
{
    int pos = (int)outBuf.Length;
    if (tag == "head") headPos = pos;
    outBuf.Write(data);
    while (outBuf.Length % 4 != 0) outBuf.WriteByte(0);
    var rec = 12 + ti * 16;
    Encoding.ASCII.GetBytes(tag).CopyTo(dir, rec);
    W32(dir, rec + 4, Checksum(data)); W32(dir, rec + 8, (uint)pos); W32(dir, rec + 12, (uint)data.Length);
    ti++;
}
var result = outBuf.ToArray();
dir.CopyTo(result, 0);
W32(result, headPos + 8, 0xB1B0AFBA - Checksum(result));
File.WriteAllBytes(args[2], result);
Console.WriteLine($"{Path.GetFileName(args[2])}: 字 {wanted.Count} / 字形 {keep.Count}(元 {numGlyphs})/ {result.Length} バイト");
if (missing.Count > 0)
{
    Console.Error.WriteLine($"元のフォントに無い字が {missing.Count} 字: " + string.Concat(missing.Take(40).Select(char.ConvertFromUtf32)));
    return 2;
}
return 0;

// 合成字形の部品(部品の字形番号と、その番号が書かれている位置)
IEnumerable<(int gid, int pos)> Components(int g)
{
    int o = GlyphOff(g), len = GlyphOff(g + 1) - o;
    if (len <= 0 || (short)U16(glyf, o) >= 0) yield break;
    int p = 10;
    while (true)
    {
        int flags = U16(glyf, o + p);
        yield return (U16(glyf, o + p + 2), p + 2);
        p += 4 + ((flags & 0x0001) != 0 ? 4 : 2);
        if ((flags & 0x0008) != 0) p += 2; else if ((flags & 0x0040) != 0) p += 4; else if ((flags & 0x0080) != 0) p += 8;
        if ((flags & 0x0020) == 0) break;
    }
}

static int U16(byte[] b, int o) => b[o] << 8 | b[o + 1];
static uint U32(byte[] b, int o) => (uint)(b[o] << 24 | b[o + 1] << 16 | b[o + 2] << 8 | b[o + 3]);
static void W16(byte[] b, int o, int v) { b[o] = (byte)(v >> 8); b[o + 1] = (byte)v; }
static void W32(byte[] b, int o, uint v) { b[o] = (byte)(v >> 24); b[o + 1] = (byte)(v >> 16); b[o + 2] = (byte)(v >> 8); b[o + 3] = (byte)v; }
static uint Checksum(byte[] d)
{
    uint sum = 0;
    for (int i = 0; i < d.Length; i += 4)
    {
        uint v = 0;
        for (int k = 0; k < 4; k++) v = (v << 8) | (i + k < d.Length ? d[i + k] : 0u);
        sum += v;
    }
    return sum;
}
