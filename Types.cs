public enum CardValue
{
    Hidden = 0,
    Two = 2,
    Three = 3,
    Four = 4,
    Five = 5,
    Six = 6,
    Seven = 7,
    Eight = 8,
    Nine = 9,
    Ten = 10,
    Jack = 10,
    Queen = 10,
    King = 10,
    Ace = 11,
}

public record Card
{
    public CardValue Value { get; init; }

    public static Card FromString(string card)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(card.ToLower());
        // Console.WriteLine($"Card '{card}' bytes: {string.Join(", ", bytes)}");
        
        // Handle full word strings first
        return card.ToLower() switch
        {
            "two" => new Card { Value = CardValue.Two },
            "three" => new Card { Value = CardValue.Three },
            "four" => new Card { Value = CardValue.Four },
            "five" => new Card { Value = CardValue.Five },
            "six" => new Card { Value = CardValue.Six },
            "seven" => new Card { Value = CardValue.Seven },
            "eight" => new Card { Value = CardValue.Eight },
            "nine" => new Card { Value = CardValue.Nine },
            "ten" => new Card { Value = CardValue.Ten },
            "jack" => new Card { Value = CardValue.Jack },
            "queen" => new Card { Value = CardValue.Queen },
            "king" => new Card { Value = CardValue.King },
            "ace" => new Card { Value = CardValue.Ace },
            "hidden" => new Card { Value = CardValue.Hidden },
            _ => // If not a word, try single character
                bytes[0] switch
                {
                    (byte)'2' => new Card { Value = CardValue.Two },
                    (byte)'3' => new Card { Value = CardValue.Three },
                    (byte)'4' => new Card { Value = CardValue.Four },
                    (byte)'5' => new Card { Value = CardValue.Five },
                    (byte)'6' => new Card { Value = CardValue.Six },
                    (byte)'7' => new Card { Value = CardValue.Seven },
                    (byte)'8' => new Card { Value = CardValue.Eight },
                    (byte)'9' => new Card { Value = CardValue.Nine },
                    (byte)'1' => new Card { Value = CardValue.Ten },  // Assuming '1' is for '10'
                    (byte)'j' => new Card { Value = CardValue.Jack },
                    (byte)'q' => new Card { Value = CardValue.Queen },
                    (byte)'k' => new Card { Value = CardValue.King },
                    (byte)'a' => new Card { Value = CardValue.Ace },
                    (byte)'h' => new Card { Value = CardValue.Hidden },
                    _ => throw new ArgumentException($"Invalid card value: {card} (first byte: {bytes[0]})")
                }
        };
    }
}

public record GameState
{
    public IReadOnlyList<Card> DealerHand { get; }
    public IReadOnlyList<Card> PlayerHand { get; }

    public GameState(IEnumerable<string> dealerCards, IEnumerable<string> playerCards)
    {
        DealerHand = dealerCards.Select(Card.FromString).ToList();
        PlayerHand = playerCards.Select(Card.FromString).ToList();
    }

    public int CalculateHandValue(IEnumerable<Card> cards)
    {
        var aces = cards.Count(c => c.Value == CardValue.Ace);
        var sum = cards.Sum(c => (int)c.Value);

        // Adjust for aces
        while (sum > 21 && aces > 0)
        {
            sum -= 10; // Convert Ace from 11 to 1
            aces--;
        }

        return sum;
    }

    public override string ToString()
    {
        return $"Dealer: {string.Join(", ", DealerHand.Select(c => c.Value))} (Score: {CalculateHandValue(DealerHand)}) | Player: {string.Join(", ", PlayerHand.Select(c => c.Value))} (Score: {CalculateHandValue(PlayerHand)})";
    }

    public override int GetHashCode()
    {
        var dealerHandHash = DealerHand.OrderBy(c => c.Value).Aggregate(0, HashCode.Combine);
        var playerHandHash = PlayerHand.OrderBy(c => c.Value).Aggregate(0, HashCode.Combine);
        return HashCode.Combine(dealerHandHash, playerHandHash);
    }

    public virtual bool Equals(GameState? other)
    {
        if (other == null) return false;
        
        var sortedDealerHand = DealerHand.OrderBy(c => c.Value).ToList();
        var sortedPlayerHand = PlayerHand.OrderBy(c => c.Value).ToList();
        var otherSortedDealerHand = other.DealerHand.OrderBy(c => c.Value).ToList();
        var otherSortedPlayerHand = other.PlayerHand.OrderBy(c => c.Value).ToList();

        return sortedDealerHand.SequenceEqual(otherSortedDealerHand) && 
               sortedPlayerHand.SequenceEqual(otherSortedPlayerHand);
    }
}