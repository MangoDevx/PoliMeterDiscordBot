using PoliMeterDiscordBot.Models;

namespace PoliMeterDiscordBot.Services;

public class DatasetBuilderService
{
    private readonly Random _rand = new();

    /// <summary>
    /// Pick up to the last 5 messages per user, expand to adjacent same-user blocks
    /// plus one prior different-user message (recursively), and return chronological.
    /// </summary>
    public List<DatabaseRecords.MessageData> BuildDataset(
        List<DatabaseRecords.MessageData> allMessages)
    {
        // 1) Order
        var msgs = allMessages.OrderBy(m => m.Timestamp).ToList();

        // 2) Seed picks: last 5 msgs per user (random if >5)
        var seeds = msgs
            .GroupBy(m => m.UserId)
            .SelectMany(grp =>
            {
                var lastFive = grp.TakeLast(5).ToList();
                return lastFive.Count > 5
                    ? lastFive.OrderBy(_ => _rand.Next()).Take(5)
                    : lastFive;
            })
            .ToList();

        // 3) Expand indices
        var pickedIndices = new HashSet<int>();
        foreach (var idx in seeds.Select(seed => msgs.FindIndex(m =>
                     m.Id == seed.Id && m.Timestamp == seed.Timestamp)).Where(idx => idx >= 0))
        {
            ExpandBlock(idx, msgs, pickedIndices);
        }

        // 4) Return the selected messages in order
        return pickedIndices
            .OrderBy(i => i)
            .Select(i => msgs[i])
            .ToList();
    }

    private void ExpandBlock(
        int idx,
        List<DatabaseRecords.MessageData> msgs,
        HashSet<int> picked)
    {
        if (idx < 0 || idx >= msgs.Count)
            return;

        var user = msgs[idx].UserId;

        // a) expand contiguous same-user block
        int start = idx, end = idx;
        while (start - 1 >= 0 && msgs[start - 1].UserId == user) start--;
        while (end + 1 < msgs.Count && msgs[end + 1].UserId == user) end++;

        for (var i = start; i <= end; i++)
            picked.Add(i);

        // b) include one prior different-user message, recurse
        var prior = start - 1;
        if (prior < 0 || msgs[prior].UserId == user) return;
        if (picked.Add(prior))
            ExpandBlock(prior, msgs, picked);
    }
}