from sts2_gym.game_rng import DotNetRandom, _int32

seed = 1672722237
rng = DotNetRandom(_int32(seed))

print("First 10 next_int(12) calls:")
for _ in range(10):
    print(rng.next_int(12))

deck = list(range(12))
for i in range(len(deck) - 1, 0, -1):
    j = rng.next_int(i + 1)
    print(f"i={i}, j={j}")
    deck[i], deck[j] = deck[j], deck[i]
print(f"Final deck: {deck}")
