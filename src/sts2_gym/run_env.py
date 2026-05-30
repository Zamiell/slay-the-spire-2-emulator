"""Run-level Gymnasium wrapper for simplified full-run training."""

from __future__ import annotations

import ctypes
from dataclasses import dataclass, field

import gymnasium as gym
import numpy as np
from gymnasium import spaces

from . import native
from .env import ENCOUNTER_NAMES, MAX_ACTIONS
from .game_rng import (
    DotNetRandom,
    GameRng,
    PlayerRngSet,
    RunRngSet,
    _int32,
    _uint32,
    get_deterministic_hash_code,
)

REWARD_SKIP_ACTION = 3
REST_HEAL_ACTION = 0
REST_UPGRADE_ACTION = 1
SHOP_CARD_ACTIONS = range(0, 7)
SHOP_RELIC_ACTIONS = range(7, 10)
SHOP_POTION_ACTIONS = range(10, 13)
SHOP_REMOVE_ACTION = 13
SHOP_SKIP_ACTION = 14
EVENT_SKIP_ACTION = 3
NEOW_SKIP_ACTION = 3
MAP_CHOICES = 4
RUN_EXTRA_OBS = 35
RUN_OBS_SIZE = native.OBS_SIZE + RUN_EXTRA_OBS
RUN_MAX_EPISODE_STEPS = 1000

PHASE_COMBAT = 0
PHASE_CARD_REWARD = 1
PHASE_MAP = 2
PHASE_REST = 3
PHASE_SHOP = 4
PHASE_RELIC_REWARD = 5
PHASE_COMPLETE = 6
PHASE_EVENT = 7
PHASE_ANCIENT = 8
PHASE_TRANSFORM_SELECT = 9  # card-selection screen for transform relics (e.g. New Leaf)

NODE_NONE = 0
NODE_NORMAL = 1
NODE_ELITE = 2
NODE_REST = 3
NODE_SHOP = 4
NODE_RELIC = 5
NODE_BOSS = 6
NODE_EVENT = 7

ACT_OVERGROWTH = 1
ACT_UNDERDOCKS = 2

MAP_WIDTH = 7
MAP_PATH_ITERATIONS = 7
MAP_BOSS_ROW = 16
MAP_TREASURE_ROW = MAP_BOSS_ROW - 7
MAP_FINAL_REST_ROW = MAP_BOSS_ROW - 1
MAP_START_COORD = (MAP_WIDTH // 2, 0)
MAP_BOSS_COORD = (MAP_WIDTH // 2, MAP_BOSS_ROW)
MAP_NODE_TO_OBS = {
    "Monster": NODE_NORMAL,
    "Elite": NODE_ELITE,
    "RestSite": NODE_REST,
    "Shop": NODE_SHOP,
    "Treasure": NODE_RELIC,
    "Unknown": NODE_EVENT,
    "Boss": NODE_BOSS,
}
MAP_LOWER_RESTRICTED = {"RestSite", "Elite"}
MAP_UPPER_RESTRICTED = {"RestSite"}
MAP_ADJACENCY_RESTRICTED = {"Elite", "RestSite", "Treasure", "Shop"}
MAP_SIBLING_RESTRICTED = {"RestSite", "Monster", "Unknown", "Elite", "Shop"}

RELIC_BURNING_BLOOD = 36
RELIC_ANCHOR = 4
RELIC_AMETHYST_AUBERGINE = 3
RELIC_ARCANE_SCROLL = 5
RELIC_BAG_OF_MARBLES = 9
RELIC_BLOOD_VIAL = 23
RELIC_BOOMING_CONCH = 29
RELIC_CAPTAINS_WHEEL = 41
RELIC_CURSED_PEARL = 54
RELIC_FISHING_ROD = 89
RELIC_GOLDEN_PEARL = 105
RELIC_HAPPY_FLOWER = 110
RELIC_HEFTY_TABLET = 111
RELIC_HORN_CLEAT = 114
RELIC_KALEIDOSCOPE = 124
RELIC_LANTERN = 128
RELIC_LARGE_CAPSULE = 129
RELIC_LAVA_ROCK = 132
RELIC_LEAD_PAPERWEIGHT = 133
RELIC_LEAFY_POULTICE = 134
RELIC_LEES_WAFFLE = 135
RELIC_LOST_COFFER = 140
RELIC_MANGO = 144
RELIC_MASSIVE_SCROLL = 145
RELIC_MEAT_ON_THE_BONE = 149
RELIC_NEOWS_BONES = 161
RELIC_NEOWS_TALISMAN = 162
RELIC_NEOWS_TORMENT = 163
RELIC_NEW_LEAF = 164
RELIC_NUTRITIOUS_OYSTER = 167
RELIC_PANTOGRAPH = 186
RELIC_PHIAL_HOLSTER = 195
RELIC_POMANDER = 201
RELIC_PRECARIOUS_SHEARS = 205
RELIC_PRECISE_SCISSORS = 206
RELIC_RED_SKULL = 215
RELIC_SCROLL_BOXES = 231
RELIC_SILKEN_TRESS = 239
RELIC_SILVER_CRUCIBLE = 240
RELIC_SMALL_CAPSULE = 242
RELIC_STRAWBERRY = 252
RELIC_VAJRA = 279
RELIC_ODDLY_SMOOTH_STONE = 169
RELIC_OLD_COIN = 170
RELIC_ORICHALCUM = 172
RELIC_BAG_OF_PREPARATION = 10
RELIC_BLACK_BLOOD = 19
RELIC_PEAR = 190
RELIC_STONE_HUMIDIFIER = 250
RELIC_VENERABLE_TEA_SET = 282
RELIC_VENERABLE_TEA_SET_ACTIVE = 100282
RELIC_WAR_HAMMER = 286
RELIC_WINGED_BOOTS = 293

# Ancient Relics (STS1-style Boss Relics from Darv)
RELIC_ASTROLABE = 1332
RELIC_BLACK_STAR = 1344
RELIC_CALLING_BELL = 1363
RELIC_EMPTY_CAGE = 1399
RELIC_PANDORAS_BOX = 1510
RELIC_RUNIC_PYRAMID = 1552
RELIC_SNECKO_EYE = 1568
RELIC_ECTOPLASM = 1395
RELIC_SOZU = 1570
RELIC_PHILOSOPHERS_STONE = 1521
RELIC_VELVET_CHOKER = 1606
RELIC_DUSTY_TOME = 1394

# Other Ancient Relics (from character ancients)
RELIC_ELECTRIC_SHRYMP = 1396
RELIC_GLASS_EYE = 1426
RELIC_SAND_CASTLE = 1554
RELIC_ALCHEMICAL_COFFER = 1326
RELIC_DRIFTWOOD = 1393
RELIC_RADIANT_PEARL = 1536
RELIC_SEA_GLASS = 1557
RELIC_PRISMATIC_GEM = 1533

# Ancient Event IDs
ANCIENT_NEOW = 1
ANCIENT_DARV = 2
ANCIENT_OROBAS = 3
ANCIENT_PAEL = 4
ANCIENT_TEZCATARA = 5

NEOWS_FURY_CARD = 321
CURSE_PLACEHOLDER_CARD = 10001

CARD_RARITY_BASIC = 0
CARD_RARITY_COMMON = 1
CARD_RARITY_UNCOMMON = 2
CARD_RARITY_RARE = 3
CARD_RARITY_ANCIENT = 4
CARD_RARITY_EVENT = 5
CARD_RARITY_STATUS = 6
CARD_RARITY_TOKEN = 7
CARD_RARITY_CURSE = 8

CARD_RARITY_ORDER = (CARD_RARITY_COMMON, CARD_RARITY_UNCOMMON, CARD_RARITY_RARE)

# Ascension 10+ (Scarcity) values from CardRarityOdds.cs
CARD_RARITY_BASE_OFFSET = -0.05
CARD_RARITY_MAX_OFFSET = 0.4
CARD_RARITY_GROWTH = 0.005  # 0.005 if Scarcity, 0.01 otherwise

CARD_RARITY_ODDS_REGULAR = (0.0149, 0.37)  # (Rare, Uncommon) base odds
CARD_RARITY_ODDS_ELITE = (0.05, 0.4)
CARD_RARITY_ODDS_BOSS = (1.0, 0.0)
CARD_RARITY_ODDS_SHOP = (0.045, 0.37)
CARD_RARITY_ODDS_UNIFORM = (0.33, 0.33)
CARD_RARITY_BY_ID = {
    1: CARD_RARITY_RARE,
    2: CARD_RARITY_RARE,
    3: CARD_RARITY_UNCOMMON,
    4: CARD_RARITY_UNCOMMON,
    5: CARD_RARITY_RARE,
    6: CARD_RARITY_RARE,
    7: CARD_RARITY_RARE,
    8: CARD_RARITY_COMMON,
    9: CARD_RARITY_RARE,
    10: CARD_RARITY_RARE,
    11: CARD_RARITY_UNCOMMON,
    12: CARD_RARITY_RARE,
    13: CARD_RARITY_COMMON,
    14: CARD_RARITY_RARE,
    15: CARD_RARITY_COMMON,
    16: CARD_RARITY_ANCIENT,
    17: CARD_RARITY_ANCIENT,
    18: CARD_RARITY_COMMON,
    19: CARD_RARITY_RARE,
    10001: CARD_RARITY_CURSE,
    20: CARD_RARITY_UNCOMMON,
    21: CARD_RARITY_RARE,
    22: CARD_RARITY_COMMON,
    23: CARD_RARITY_UNCOMMON,
    24: CARD_RARITY_COMMON,
    25: CARD_RARITY_UNCOMMON,
    26: CARD_RARITY_COMMON,
    27: CARD_RARITY_RARE,
    28: CARD_RARITY_COMMON,
    29: CARD_RARITY_RARE,
    30: CARD_RARITY_BASIC,
    31: CARD_RARITY_UNCOMMON,
    32: CARD_RARITY_RARE,
    33: CARD_RARITY_COMMON,
    34: CARD_RARITY_RARE,
    35: CARD_RARITY_RARE,
    36: CARD_RARITY_STATUS,
    37: CARD_RARITY_COMMON,
    38: CARD_RARITY_UNCOMMON,
    39: CARD_RARITY_ANCIENT,
    40: CARD_RARITY_RARE,
    41: CARD_RARITY_UNCOMMON,
    42: CARD_RARITY_COMMON,
    43: CARD_RARITY_RARE,
    44: CARD_RARITY_COMMON,
    45: CARD_RARITY_COMMON,
    46: CARD_RARITY_COMMON,
    47: CARD_RARITY_UNCOMMON,
    48: CARD_RARITY_UNCOMMON,
    49: CARD_RARITY_BASIC,
    50: CARD_RARITY_COMMON,
    51: CARD_RARITY_RARE,
    52: CARD_RARITY_RARE,
    53: CARD_RARITY_UNCOMMON,
    54: CARD_RARITY_COMMON,
    55: CARD_RARITY_UNCOMMON,
    56: CARD_RARITY_UNCOMMON,
    57: CARD_RARITY_UNCOMMON,
    58: CARD_RARITY_RARE,
    59: CARD_RARITY_ANCIENT,
    60: CARD_RARITY_COMMON,
    61: CARD_RARITY_ANCIENT,
    62: CARD_RARITY_UNCOMMON,
    63: CARD_RARITY_RARE,
    64: CARD_RARITY_UNCOMMON,
    65: CARD_RARITY_RARE,
    66: CARD_RARITY_UNCOMMON,
    67: CARD_RARITY_UNCOMMON,
    68: CARD_RARITY_RARE,
    69: CARD_RARITY_UNCOMMON,
    70: CARD_RARITY_RARE,
    71: CARD_RARITY_UNCOMMON,
    72: CARD_RARITY_EVENT,
    73: CARD_RARITY_RARE,
    74: CARD_RARITY_UNCOMMON,
    75: CARD_RARITY_UNCOMMON,
    76: CARD_RARITY_RARE,
    77: CARD_RARITY_EVENT,
    78: CARD_RARITY_UNCOMMON,
    79: CARD_RARITY_UNCOMMON,
    80: CARD_RARITY_UNCOMMON,
    81: CARD_RARITY_COMMON,
    82: CARD_RARITY_UNCOMMON,
    83: CARD_RARITY_UNCOMMON,
    84: CARD_RARITY_COMMON,
    85: CARD_RARITY_UNCOMMON,
    86: CARD_RARITY_UNCOMMON,
    87: CARD_RARITY_COMMON,
    88: CARD_RARITY_EVENT,
    89: CARD_RARITY_COMMON,
    90: CARD_RARITY_UNCOMMON,
    91: CARD_RARITY_COMMON,
    92: CARD_RARITY_COMMON,
    93: CARD_RARITY_COMMON,
    94: CARD_RARITY_COMMON,
    95: CARD_RARITY_UNCOMMON,
    96: CARD_RARITY_RARE,
    97: CARD_RARITY_UNCOMMON,
    98: CARD_RARITY_COMMON,
    99: CARD_RARITY_RARE,
    100: CARD_RARITY_UNCOMMON,
    101: CARD_RARITY_RARE,
    102: CARD_RARITY_UNCOMMON,
    103: CARD_RARITY_RARE,
    104: CARD_RARITY_COMMON,
    105: CARD_RARITY_UNCOMMON,
    106: CARD_RARITY_RARE,
    107: CARD_RARITY_ANCIENT,
    108: CARD_RARITY_COMMON,
    109: CARD_RARITY_UNCOMMON,
    110: CARD_RARITY_RARE,
    111: CARD_RARITY_RARE,
    112: CARD_RARITY_COMMON,
    113: CARD_RARITY_RARE,
    114: CARD_RARITY_RARE,
    115: CARD_RARITY_COMMON,
    116: CARD_RARITY_COMMON,
    117: CARD_RARITY_COMMON,
    118: CARD_RARITY_UNCOMMON,
    119: CARD_RARITY_RARE,
    120: CARD_RARITY_UNCOMMON,
    121: CARD_RARITY_UNCOMMON,
    122: CARD_RARITY_UNCOMMON,
    10002: CARD_RARITY_STATUS,
    10008: CARD_RARITY_STATUS,
    10009: CARD_RARITY_STATUS,
    10010: CARD_RARITY_STATUS,
    10011: CARD_RARITY_STATUS,
    10012: CARD_RARITY_STATUS,
    123: CARD_RARITY_COMMON,
    124: CARD_RARITY_UNCOMMON,
    125: CARD_RARITY_UNCOMMON,
    126: CARD_RARITY_UNCOMMON,
    127: CARD_RARITY_UNCOMMON,
    128: CARD_RARITY_STATUS,
    129: CARD_RARITY_RARE,
    130: CARD_RARITY_BASIC,
    131: CARD_RARITY_BASIC,
    132: CARD_RARITY_BASIC,
    133: CARD_RARITY_BASIC,
    134: CARD_RARITY_BASIC,
    135: CARD_RARITY_COMMON,
    136: CARD_RARITY_COMMON,
    137: CARD_RARITY_RARE,
    138: CARD_RARITY_COMMON,
    139: CARD_RARITY_UNCOMMON,
    140: CARD_RARITY_RARE,
    141: CARD_RARITY_RARE,
    142: CARD_RARITY_UNCOMMON,
    143: CARD_RARITY_UNCOMMON,
    144: CARD_RARITY_RARE,
    145: CARD_RARITY_UNCOMMON,
    146: CARD_RARITY_UNCOMMON,
    147: CARD_RARITY_UNCOMMON,
    148: CARD_RARITY_EVENT,
    149: CARD_RARITY_COMMON,
    150: CARD_RARITY_UNCOMMON,
    151: CARD_RARITY_UNCOMMON,
    152: CARD_RARITY_COMMON,
    153: CARD_RARITY_UNCOMMON,
    154: CARD_RARITY_UNCOMMON,
    155: CARD_RARITY_UNCOMMON,
    156: CARD_RARITY_BASIC,
    157: CARD_RARITY_EVENT,
    158: CARD_RARITY_RARE,
    159: CARD_RARITY_RARE,
    160: CARD_RARITY_RARE,
    161: CARD_RARITY_RARE,
    162: CARD_RARITY_RARE,
    163: CARD_RARITY_UNCOMMON,
    164: CARD_RARITY_UNCOMMON,
    165: CARD_RARITY_EVENT,
    166: CARD_RARITY_CURSE,
    167: CARD_RARITY_EVENT,
    168: CARD_RARITY_RARE,
    169: CARD_RARITY_RARE,
    170: CARD_RARITY_UNCOMMON,
    171: CARD_RARITY_RARE,
    172: CARD_RARITY_UNCOMMON,
    173: CARD_RARITY_RARE,
    174: CARD_RARITY_UNCOMMON,
    175: CARD_RARITY_UNCOMMON,
    176: CARD_RARITY_UNCOMMON,
    177: CARD_RARITY_UNCOMMON,
    178: CARD_RARITY_EVENT,
    179: CARD_RARITY_BASIC,
    180: CARD_RARITY_RARE,
    181: CARD_RARITY_UNCOMMON,
    182: CARD_RARITY_COMMON,
    183: CARD_RARITY_RARE,
    184: CARD_RARITY_EVENT,
    185: CARD_RARITY_UNCOMMON,
    186: CARD_RARITY_UNCOMMON,
    187: CARD_RARITY_UNCOMMON,
    188: CARD_RARITY_RARE,
    189: CARD_RARITY_UNCOMMON,
    190: CARD_RARITY_UNCOMMON,
    191: CARD_RARITY_UNCOMMON,
    192: CARD_RARITY_UNCOMMON,
    193: CARD_RARITY_UNCOMMON,
    194: CARD_RARITY_RARE,
    195: CARD_RARITY_UNCOMMON,
    196: CARD_RARITY_UNCOMMON,
    197: CARD_RARITY_UNCOMMON,
    198: CARD_RARITY_COMMON,
    199: CARD_RARITY_UNCOMMON,
    200: CARD_RARITY_COMMON,
    201: CARD_RARITY_COMMON,
    202: CARD_RARITY_UNCOMMON,
    203: CARD_RARITY_ANCIENT,
    204: CARD_RARITY_RARE,
    205: CARD_RARITY_UNCOMMON,
    206: CARD_RARITY_STATUS,
    207: CARD_RARITY_UNCOMMON,
    208: CARD_RARITY_UNCOMMON,
    209: CARD_RARITY_TOKEN,
    210: CARD_RARITY_UNCOMMON,
    211: CARD_RARITY_UNCOMMON,
    212: CARD_RARITY_UNCOMMON,
    213: CARD_RARITY_UNCOMMON,
    214: CARD_RARITY_COMMON,
    215: CARD_RARITY_RARE,
    216: CARD_RARITY_RARE,
    217: CARD_RARITY_TOKEN,
    218: CARD_RARITY_UNCOMMON,
    219: CARD_RARITY_UNCOMMON,
    220: CARD_RARITY_UNCOMMON,
    221: CARD_RARITY_RARE,
    222: CARD_RARITY_COMMON,
    223: CARD_RARITY_COMMON,
    224: CARD_RARITY_COMMON,
    225: CARD_RARITY_RARE,
    226: CARD_RARITY_RARE,
    227: CARD_RARITY_COMMON,
    228: CARD_RARITY_COMMON,
    229: CARD_RARITY_RARE,
    230: CARD_RARITY_COMMON,
    231: CARD_RARITY_COMMON,
    232: CARD_RARITY_UNCOMMON,
    233: CARD_RARITY_RARE,
    234: CARD_RARITY_RARE,
    235: CARD_RARITY_UNCOMMON,
    236: CARD_RARITY_RARE,
    237: CARD_RARITY_UNCOMMON,
    238: CARD_RARITY_COMMON,
    239: CARD_RARITY_UNCOMMON,
    240: CARD_RARITY_COMMON,
    241: CARD_RARITY_RARE,
    242: CARD_RARITY_UNCOMMON,
    243: CARD_RARITY_RARE,
    244: CARD_RARITY_RARE,
    245: CARD_RARITY_EVENT,
    246: CARD_RARITY_RARE,
    247: CARD_RARITY_UNCOMMON,
    248: CARD_RARITY_COMMON,
    249: CARD_RARITY_UNCOMMON,
    250: CARD_RARITY_RARE,
    251: CARD_RARITY_UNCOMMON,
    252: CARD_RARITY_COMMON,
    253: CARD_RARITY_COMMON,
    254: CARD_RARITY_UNCOMMON,
    255: CARD_RARITY_UNCOMMON,
    256: CARD_RARITY_RARE,
    257: CARD_RARITY_RARE,
    258: CARD_RARITY_RARE,
    259: CARD_RARITY_RARE,
    260: CARD_RARITY_UNCOMMON,
    261: CARD_RARITY_RARE,
    262: CARD_RARITY_UNCOMMON,
    263: CARD_RARITY_UNCOMMON,
    264: CARD_RARITY_UNCOMMON,
    265: CARD_RARITY_UNCOMMON,
    266: CARD_RARITY_UNCOMMON,
    267: CARD_RARITY_COMMON,
    268: CARD_RARITY_COMMON,
    269: CARD_RARITY_UNCOMMON,
    270: CARD_RARITY_UNCOMMON,
    271: CARD_RARITY_RARE,
    272: CARD_RARITY_RARE,
    273: CARD_RARITY_UNCOMMON,
    274: CARD_RARITY_UNCOMMON,
    275: CARD_RARITY_UNCOMMON,
    276: CARD_RARITY_RARE,
    277: CARD_RARITY_RARE,
    278: CARD_RARITY_UNCOMMON,
    279: CARD_RARITY_COMMON,
    280: CARD_RARITY_UNCOMMON,
    281: CARD_RARITY_COMMON,
    282: CARD_RARITY_COMMON,
    283: CARD_RARITY_UNCOMMON,
    284: CARD_RARITY_UNCOMMON,
    285: CARD_RARITY_UNCOMMON,
    286: CARD_RARITY_UNCOMMON,
    287: CARD_RARITY_COMMON,
    288: CARD_RARITY_UNCOMMON,
    289: CARD_RARITY_TOKEN,
    290: CARD_RARITY_UNCOMMON,
    291: CARD_RARITY_RARE,
    292: CARD_RARITY_EVENT,
    293: CARD_RARITY_RARE,
    294: CARD_RARITY_RARE,
    295: CARD_RARITY_RARE,
    296: CARD_RARITY_UNCOMMON,
    297: CARD_RARITY_RARE,
    298: CARD_RARITY_RARE,
    299: CARD_RARITY_ANCIENT,
    300: CARD_RARITY_RARE,
    301: CARD_RARITY_UNCOMMON,
    302: CARD_RARITY_UNCOMMON,
    303: CARD_RARITY_EVENT,
    304: CARD_RARITY_ANCIENT,
    305: CARD_RARITY_RARE,
    306: CARD_RARITY_RARE,
    307: CARD_RARITY_UNCOMMON,
    308: CARD_RARITY_TOKEN,
    309: CARD_RARITY_TOKEN,
    310: CARD_RARITY_TOKEN,
    311: CARD_RARITY_UNCOMMON,
    312: CARD_RARITY_RARE,
    313: CARD_RARITY_COMMON,
    314: CARD_RARITY_COMMON,
    315: CARD_RARITY_RARE,
    316: CARD_RARITY_UNCOMMON,
    317: CARD_RARITY_RARE,
    318: CARD_RARITY_RARE,
    319: CARD_RARITY_RARE,
    320: CARD_RARITY_COMMON,
    321: CARD_RARITY_ANCIENT,
    322: CARD_RARITY_RARE,
    323: CARD_RARITY_BASIC,
    324: CARD_RARITY_RARE,
    325: CARD_RARITY_RARE,
    326: CARD_RARITY_UNCOMMON,
    327: CARD_RARITY_RARE,
    328: CARD_RARITY_RARE,
    329: CARD_RARITY_UNCOMMON,
    330: CARD_RARITY_UNCOMMON,
    331: CARD_RARITY_RARE,
    332: CARD_RARITY_RARE,
    333: CARD_RARITY_UNCOMMON,
    334: CARD_RARITY_RARE,
    335: CARD_RARITY_UNCOMMON,
    336: CARD_RARITY_UNCOMMON,
    337: CARD_RARITY_EVENT,
    338: CARD_RARITY_UNCOMMON,
    339: CARD_RARITY_RARE,
    340: CARD_RARITY_UNCOMMON,
    341: CARD_RARITY_UNCOMMON,
    342: CARD_RARITY_UNCOMMON,
    343: CARD_RARITY_UNCOMMON,
    344: CARD_RARITY_UNCOMMON,
    345: CARD_RARITY_UNCOMMON,
    346: CARD_RARITY_UNCOMMON,
    347: CARD_RARITY_COMMON,
    348: CARD_RARITY_EVENT,
    349: CARD_RARITY_COMMON,
    350: CARD_RARITY_UNCOMMON,
    351: CARD_RARITY_COMMON,
    352: CARD_RARITY_COMMON,
    353: CARD_RARITY_UNCOMMON,
    354: CARD_RARITY_UNCOMMON,
    355: CARD_RARITY_UNCOMMON,
    356: CARD_RARITY_COMMON,
    357: CARD_RARITY_COMMON,
    358: CARD_RARITY_COMMON,
    359: CARD_RARITY_UNCOMMON,
    360: CARD_RARITY_UNCOMMON,
    361: CARD_RARITY_COMMON,
    362: CARD_RARITY_COMMON,
    363: CARD_RARITY_UNCOMMON,
    364: CARD_RARITY_RARE,
    365: CARD_RARITY_UNCOMMON,
    366: CARD_RARITY_UNCOMMON,
    367: CARD_RARITY_UNCOMMON,
    368: CARD_RARITY_ANCIENT,
    369: CARD_RARITY_UNCOMMON,
    370: CARD_RARITY_COMMON,
    371: CARD_RARITY_UNCOMMON,
    372: CARD_RARITY_UNCOMMON,
    373: CARD_RARITY_UNCOMMON,
    374: CARD_RARITY_RARE,
    375: CARD_RARITY_ANCIENT,
    376: CARD_RARITY_UNCOMMON,
    377: CARD_RARITY_UNCOMMON,
    378: CARD_RARITY_UNCOMMON,
    379: CARD_RARITY_RARE,
    380: CARD_RARITY_RARE,
    381: CARD_RARITY_UNCOMMON,
    382: CARD_RARITY_UNCOMMON,
    383: CARD_RARITY_RARE,
    384: CARD_RARITY_COMMON,
    385: CARD_RARITY_RARE,
    386: CARD_RARITY_COMMON,
    387: CARD_RARITY_RARE,
    388: CARD_RARITY_EVENT,
    389: CARD_RARITY_COMMON,
    390: CARD_RARITY_UNCOMMON,
    391: CARD_RARITY_UNCOMMON,
    392: CARD_RARITY_UNCOMMON,
    393: CARD_RARITY_ANCIENT,
    394: CARD_RARITY_RARE,
    395: CARD_RARITY_UNCOMMON,
    396: CARD_RARITY_UNCOMMON,
    397: CARD_RARITY_COMMON,
    398: CARD_RARITY_UNCOMMON,
    399: CARD_RARITY_EVENT,
    400: CARD_RARITY_UNCOMMON,
    401: CARD_RARITY_RARE,
    402: CARD_RARITY_UNCOMMON,
    403: CARD_RARITY_RARE,
    404: CARD_RARITY_UNCOMMON,
    405: CARD_RARITY_RARE,
    406: CARD_RARITY_RARE,
    407: CARD_RARITY_UNCOMMON,
    408: CARD_RARITY_UNCOMMON,
    409: CARD_RARITY_COMMON,
    410: CARD_RARITY_UNCOMMON,
    411: CARD_RARITY_RARE,
    412: CARD_RARITY_COMMON,
    413: CARD_RARITY_RARE,
    414: CARD_RARITY_UNCOMMON,
    415: CARD_RARITY_RARE,
    416: CARD_RARITY_RARE,
    417: CARD_RARITY_UNCOMMON,
    418: CARD_RARITY_RARE,
    419: CARD_RARITY_RARE,
    420: CARD_RARITY_RARE,
    421: CARD_RARITY_COMMON,
    422: CARD_RARITY_RARE,
    423: CARD_RARITY_UNCOMMON,
    424: CARD_RARITY_RARE,
    425: CARD_RARITY_UNCOMMON,
    426: CARD_RARITY_RARE,
    427: CARD_RARITY_RARE,
    428: CARD_RARITY_RARE,
    429: CARD_RARITY_UNCOMMON,
    430: CARD_RARITY_TOKEN,
    431: CARD_RARITY_UNCOMMON,
    432: CARD_RARITY_UNCOMMON,
    433: CARD_RARITY_COMMON,
    434: CARD_RARITY_UNCOMMON,
    435: CARD_RARITY_RARE,
    436: CARD_RARITY_UNCOMMON,
    437: CARD_RARITY_UNCOMMON,
    438: CARD_RARITY_UNCOMMON,
    439: CARD_RARITY_COMMON,
    440: CARD_RARITY_STATUS,
    441: CARD_RARITY_UNCOMMON,
    442: CARD_RARITY_COMMON,
    443: CARD_RARITY_COMMON,
    444: CARD_RARITY_RARE,
    445: CARD_RARITY_COMMON,
    446: CARD_RARITY_TOKEN,
    447: CARD_RARITY_RARE,
    448: CARD_RARITY_TOKEN,
    449: CARD_RARITY_COMMON,
    450: CARD_RARITY_UNCOMMON,
    451: CARD_RARITY_UNCOMMON,
    452: CARD_RARITY_RARE,
    453: CARD_RARITY_RARE,
    454: CARD_RARITY_UNCOMMON,
    455: CARD_RARITY_UNCOMMON,
    456: CARD_RARITY_COMMON,
    457: CARD_RARITY_CURSE,
    458: CARD_RARITY_UNCOMMON,
    459: CARD_RARITY_EVENT,
    460: CARD_RARITY_RARE,
    461: CARD_RARITY_EVENT,
    462: CARD_RARITY_UNCOMMON,
    463: CARD_RARITY_UNCOMMON,
    464: CARD_RARITY_RARE,
    465: CARD_RARITY_UNCOMMON,
    466: CARD_RARITY_UNCOMMON,
    467: CARD_RARITY_UNCOMMON,
    468: CARD_RARITY_RARE,
    469: CARD_RARITY_UNCOMMON,
    470: CARD_RARITY_UNCOMMON,
    471: CARD_RARITY_BASIC,
    472: CARD_RARITY_BASIC,
    473: CARD_RARITY_BASIC,
    474: CARD_RARITY_BASIC,
    475: CARD_RARITY_BASIC,
    476: CARD_RARITY_UNCOMMON,
    477: CARD_RARITY_COMMON,
    478: CARD_RARITY_UNCOMMON,
    479: CARD_RARITY_UNCOMMON,
    480: CARD_RARITY_RARE,
    481: CARD_RARITY_UNCOMMON,
    482: CARD_RARITY_ANCIENT,
    483: CARD_RARITY_BASIC,
    484: CARD_RARITY_COMMON,
    485: CARD_RARITY_TOKEN,
    486: CARD_RARITY_COMMON,
    487: CARD_RARITY_RARE,
    488: CARD_RARITY_UNCOMMON,
    489: CARD_RARITY_UNCOMMON,
    490: CARD_RARITY_UNCOMMON,
    491: CARD_RARITY_UNCOMMON,
    492: CARD_RARITY_RARE,
    493: CARD_RARITY_UNCOMMON,
    494: CARD_RARITY_RARE,
    495: CARD_RARITY_UNCOMMON,
    496: CARD_RARITY_UNCOMMON,
    497: CARD_RARITY_UNCOMMON,
    498: CARD_RARITY_UNCOMMON,
    499: CARD_RARITY_RARE,
    500: CARD_RARITY_RARE,
    501: CARD_RARITY_RARE,
    502: CARD_RARITY_ANCIENT,
    503: CARD_RARITY_RARE,
    504: CARD_RARITY_UNCOMMON,
    505: CARD_RARITY_RARE,
    506: CARD_RARITY_UNCOMMON,
    507: CARD_RARITY_UNCOMMON,
    508: CARD_RARITY_COMMON,
    509: CARD_RARITY_RARE,
    510: CARD_RARITY_RARE,
    511: CARD_RARITY_EVENT,
    512: CARD_RARITY_STATUS,
    513: CARD_RARITY_RARE,
    514: CARD_RARITY_RARE,
    515: CARD_RARITY_RARE,
    516: CARD_RARITY_COMMON,
    517: CARD_RARITY_COMMON,
    518: CARD_RARITY_COMMON,
    519: CARD_RARITY_COMMON,
    520: CARD_RARITY_RARE,
    521: CARD_RARITY_UNCOMMON,
    522: CARD_RARITY_UNCOMMON,
    523: CARD_RARITY_RARE,
    524: CARD_RARITY_BASIC,
    525: CARD_RARITY_RARE,
    526: CARD_RARITY_UNCOMMON,
    527: CARD_RARITY_COMMON,
    528: CARD_RARITY_UNCOMMON,
    529: CARD_RARITY_UNCOMMON,
    530: CARD_RARITY_COMMON,
    531: CARD_RARITY_UNCOMMON,
    532: CARD_RARITY_BASIC,
    533: CARD_RARITY_UNCOMMON,
    534: CARD_RARITY_RARE,
    535: CARD_RARITY_UNCOMMON,
    536: CARD_RARITY_RARE,
    537: CARD_RARITY_UNCOMMON,
    538: CARD_RARITY_UNCOMMON,
    539: CARD_RARITY_ANCIENT,
    540: CARD_RARITY_UNCOMMON,
    541: CARD_RARITY_ANCIENT,
    542: CARD_RARITY_COMMON,
    543: CARD_RARITY_ANCIENT,
    544: CARD_RARITY_COMMON,
    545: CARD_RARITY_BASIC,
    546: CARD_RARITY_RARE,
}

IRONCLAD_REWARD_POOL = np.array(
    [
        9, 13, 18, 20, 29, 30, 31, 46, 45, 47,
        50, 58, 59, 60, 66, 69, 546, 87, 95, 99,
        107, 113, 114, 119, 131, 141, 142, 147, 150, 155,
        174, 175, 183, 185, 188, 189, 195, 205, 238, 240,
        246, 247, 254, 261, 262, 263, 265, 268, 272, 273,
        295, 313, 328, 332, 334, 339, 349, 353, 358, 364,
        374, 378, 381, 404, 414, 421, 433, 454, 462, 464,
        465, 466, 472, 486, 492, 493, 494, 505, 508, 516,
        517, 519, 525, 526, 529, 533, 538,
    ],
    dtype=np.int32,
)
COLORLESS_REWARD_POOL = np.array(
    [
        10, 14, 23, 32, 34, 38, 51, 73, 80, 121,
        146, 153, 168, 170, 173, 181, 191, 193, 197, 213,
        225, 234, 250, 255, 260, 266, 270, 271, 277, 286,
        297, 300, 306, 307, 327, 333, 342, 343, 363, 365,
        366, 369, 372, 380, 394, 396, 401, 406, 411, 415,
        416, 417, 431, 455, 470, 491, 498, 499, 504, 506,
        521, 522, 535,
    ],
    dtype=np.int32,
)
SHOP_ATTACK_CARDS = np.array([13, 20, 50, 60, 69, 87, 147, 189, 240, 247, 254, 268, 349, 358, 421, 454, 465, 486, 508, 519, 538], dtype=np.int32)
SHOP_SKILL_CARDS = np.array([18, 31, 46, 45, 150, 155, 174, 175, 205, 238, 396, 414, 433, 455, 493, 516, 517, 521], dtype=np.int32)
SHOP_POWER_CARDS = np.array([185, 265, 273, 462, 533], dtype=np.int32)
SHOP_COLORLESS_CARDS = COLORLESS_REWARD_POOL
SHOP_COLORLESS_CARD_RARITIES = (CARD_RARITY_UNCOMMON, CARD_RARITY_RARE)
SHOP_CARD_BASE_COSTS_BY_RARITY = {
    CARD_RARITY_COMMON: 50,
    CARD_RARITY_UNCOMMON: 75,
    CARD_RARITY_RARE: 150,
}
SHOP_POTION_BASE_COSTS = {
    CARD_RARITY_COMMON: 50,
    CARD_RARITY_UNCOMMON: 75,
    CARD_RARITY_RARE: 100,
}
POTION_RARITY_COMMON = CARD_RARITY_COMMON
POTION_RARITY_UNCOMMON = CARD_RARITY_UNCOMMON
POTION_RARITY_RARE = CARD_RARITY_RARE
POTION_REWARD_BASE_ODDS = 0.4
POTION_REWARD_STEP = 0.1
POTION_REWARD_ELITE_BONUS = 0.25
POTION_RARITY_BY_ID = {
    2: POTION_RARITY_COMMON,
    3: POTION_RARITY_RARE,
    4: POTION_RARITY_UNCOMMON,
    5: POTION_RARITY_COMMON,
    6: POTION_RARITY_COMMON,
    8: POTION_RARITY_RARE,
    9: POTION_RARITY_UNCOMMON,
    10: POTION_RARITY_COMMON,
    13: POTION_RARITY_UNCOMMON,
    14: POTION_RARITY_COMMON,
    15: POTION_RARITY_RARE,
    16: POTION_RARITY_RARE,
    17: POTION_RARITY_UNCOMMON,
    18: POTION_RARITY_COMMON,
    19: POTION_RARITY_RARE,
    21: POTION_RARITY_COMMON,
    22: POTION_RARITY_RARE,
    23: POTION_RARITY_COMMON,
    24: POTION_RARITY_COMMON,
    26: POTION_RARITY_UNCOMMON,
    28: POTION_RARITY_RARE,
    29: POTION_RARITY_UNCOMMON,
    30: POTION_RARITY_UNCOMMON,
    32: POTION_RARITY_RARE,
    34: POTION_RARITY_UNCOMMON,
    36: POTION_RARITY_UNCOMMON,
    37: POTION_RARITY_RARE,
    38: POTION_RARITY_RARE,
    39: POTION_RARITY_RARE,
    40: POTION_RARITY_RARE,
    42: POTION_RARITY_UNCOMMON,
    47: POTION_RARITY_UNCOMMON,
    48: POTION_RARITY_COMMON,
    49: POTION_RARITY_UNCOMMON,
    50: POTION_RARITY_UNCOMMON,
    51: POTION_RARITY_RARE,
    52: POTION_RARITY_RARE,
    53: POTION_RARITY_COMMON,
    54: POTION_RARITY_RARE,
    56: POTION_RARITY_COMMON,
    57: POTION_RARITY_UNCOMMON,
    59: POTION_RARITY_COMMON,
    60: POTION_RARITY_COMMON,
    61: POTION_RARITY_UNCOMMON,
    62: POTION_RARITY_COMMON,
    63: POTION_RARITY_COMMON,
    1: POTION_RARITY_UNCOMMON,
    58: POTION_RARITY_RARE,
}
POTION_REWARD_POOL = np.array(
    [
        1, 2, 3, 4, 5, 6, 8, 9, 10, 13,
        14, 15, 16, 17, 18, 19, 21, 22, 23, 24,
        26, 28, 29, 30, 32, 34, 36, 37, 38, 39,
        40, 42, 47, 48, 49, 50, 51, 52, 53, 54,
        56, 57, 58, 59, 60, 61, 62, 63,
    ],
    dtype=np.int32,
)
SHOP_RELIC_BASE_COSTS = {
    RELIC_AMETHYST_AUBERGINE: 175,
    RELIC_ANCHOR: 175,
    RELIC_BAG_OF_PREPARATION: 175,
    RELIC_BAG_OF_MARBLES: 175,
    RELIC_BLOOD_VIAL: 175,
    RELIC_CAPTAINS_WHEEL: 275,
    RELIC_HAPPY_FLOWER: 175,
    RELIC_HORN_CLEAT: 225,
    RELIC_LANTERN: 175,
    RELIC_LEES_WAFFLE: 200,
    RELIC_MANGO: 275,
    RELIC_MEAT_ON_THE_BONE: 275,
    RELIC_ODDLY_SMOOTH_STONE: 175,
    RELIC_OLD_COIN: 275,
    RELIC_ORICHALCUM: 175,
    RELIC_PANTOGRAPH: 175,
    RELIC_PEAR: 225,
    RELIC_RED_SKULL: 175,
    RELIC_STONE_HUMIDIFIER: 175,
    RELIC_STRAWBERRY: 175,
    RELIC_VENERABLE_TEA_SET: 175,
    RELIC_VAJRA: 175,
    RELIC_WAR_HAMMER: 999999999,
}
RELIC_REWARD_POOL = np.array(
    [
        3, 4, 9, 10, 19, 23, 41, 110, 114, 128,
        135, 144, 149, 169, 170, 172, 186, 190, 215, 250,
        252, 279, 282, 286,
    ],
    dtype=np.int32,
)

NON_UPGRADABLE_CARD_IDS = {
    36, 128, 166, 206, 440, 457, 512, 10001, 10002, 10008, 10009, 10010, 10011, 10012,
}
UPGRADABLE_STARTER_CARDS = {30, 131, 472}
OVERGROWTH_WEAK_ENCOUNTERS = np.array([2, 3, 11, 8], dtype=np.int32)
UNDERDOCKS_WEAK_ENCOUNTERS = np.array([9, 12, 10, 13], dtype=np.int32)
_OVERGROWTH_WEAK_POOL = [8, 2, 11, 3]
_UNDERDOCKS_WEAK_POOL = [9, 12, 10, 13]
_UPFRONT_PRE_CALLS = 202
_OVERGROWTH_EVENT_SHUFFLE_CALLS = 60
_UNDERDOCKS_EVENT_SHUFFLE_CALLS = 57
_TOTAL_ENCOUNTER_SLOTS = 15
_WEAK_ENCOUNTER_SLOTS = 3
_NORMAL_ENCOUNTER_SLOTS = 12
_ELITE_ENCOUNTER_SLOTS = 15
_NICHE_HASH = _uint32(get_deterministic_hash_code("niche"))
_SHUFFLE_HASH = _uint32(get_deterministic_hash_code("shuffle"))
_MONSTER_AI_HASH = _uint32(get_deterministic_hash_code("monster_ai"))
_SLIMES_WEAK_ENCOUNTER_ID = 3
_SLIMES_WEAK_ENTRY_HASH = get_deterministic_hash_code("SLIMES_WEAK")
OVERGROWTH_NORMAL_ENCOUNTERS = np.array([5, 14, 15, 16, 17, 18, 19, 20, 21, 27, 28, 29], dtype=np.int32)
UNDERDOCKS_NORMAL_ENCOUNTERS = np.array([9, 0, 7, 6, 22, 23, 24, 25, 26, 30], dtype=np.int32)
OVERGROWTH_ELITE_ENCOUNTERS = np.array([68, 65], dtype=np.int32)
UNDERDOCKS_ELITE_ENCOUNTERS = np.array([72, 67], dtype=np.int32)
OVERGROWTH_BOSS_ENCOUNTERS = np.array([83, 74, 82], dtype=np.int32)
UNDERDOCKS_BOSS_ENCOUNTERS = np.array([84, 79, 77], dtype=np.int32)
GREMLIN_MERC_ENCOUNTER = 7
EVENT_UNREST_SITE = 1
EVENT_AROMA_OF_CHAOS = 2
EVENT_SIMPLE_REWARD = 3
EVENT_JUNGLE_MAZE_ADVENTURE = 4
EVENT_MORPHIC_GROVE = 5
EVENT_BRAIN_LEECH = 6
EVENT_THE_LEGENDS_WERE_TRUE = 7
EVENT_DOORS_OF_LIGHT_AND_DARK = 8
EVENT_SUNKEN_TREASURY = 9
EVENT_RESULT_PENDING = -1
POOR_SLEEP_CARD = 10001
SPOILS_MAP_CARD = 10002

NEOW_CURSE_OPTIONS = [54, 111, 129, 134, 161, 205, 239, 240]
NEOW_POSITIVE_OPTIONS = [5, 29, 89, 105, 124, 133, 140, 145, 163, 164, 195, 206, 231, 293]
_NEOW_REWARDS_RNG_ADVANCES = {5: 1, 195: 4, 111: 3, 133: 6, 124: 18}
STARTER_DECK = [472, 472, 472, 472, 472, 131, 131, 131, 131, 30, 10001]

@dataclass
class RunMapNode:
    col: int
    row: int
    node_type: str = "Unassigned"
    can_be_modified: bool = True
    children: list[tuple[int, int]] = field(default_factory=list)
    parents: list[tuple[int, int]] = field(default_factory=list)
    encounter_id: int = 0
class Sts2RunEnv(gym.Env):
    """Deterministic simplified full-run environment.

    Native C# combat remains the source of truth for combat. Map, rewards,
    shops, rest sites, relic rewards, and run-level state are modeled in Python
    for fast training experiments.
    """

    metadata = {"render_modes": []}

    def __init__(
        self,
        seed: int | str = 0,
        max_episode_steps: int = RUN_MAX_EPISODE_STEPS,
        max_floors: int = 16,
    ):
        super().__init__()
        self._seed = seed
        self._run_rng_set = RunRngSet(str(seed))
        self._player_rng = PlayerRngSet(self._run_rng_set)
        self._rng = np.random.default_rng(self._run_rng_set.seed)
        self._max_episode_steps = max_episode_steps
        self._max_floors = max_floors
        self._elapsed_steps = 0
        self._floor = 1
        self._phase = PHASE_ANCIENT
        self._deck = list(STARTER_DECK)
        self._gold = 99
        self._player_hp = 64
        self._player_max_hp = 80
        self._potions = [0, 0, 0]
        self._relics = [RELIC_BURNING_BLOOD]
        self._current_node_type = NODE_NORMAL
        self._pending_relic_reward = False
        self._reward_cards = np.zeros(3, dtype=np.int32)
        self._reward_upgraded = np.zeros(3, dtype=bool)
        self._shop_cards = np.zeros(7, dtype=np.int32)
        self._shop_relics = np.zeros(3, dtype=np.int32)
        self._shop_potions = np.zeros(3, dtype=np.int32)
        self._shop_costs = np.zeros(14, dtype=np.int32)
        self._map_node_types = np.zeros(MAP_CHOICES, dtype=np.int32)
        self._map_choices = np.zeros(MAP_CHOICES, dtype=np.int32)
        self._relic_reward = 0
        self._neow_options = np.zeros(3, dtype=np.int32)
        self._silver_crucible_card_rewards_seen = 0
        self._silver_crucible_treasure_seen = 0
        self._fishing_rod_combats_seen = 0
        self._venerable_tea_set_active = False
        self._winged_boots_times_used = 0
        self._shop_removals_used = 0
        self._niche_calls_consumed = 0
        self._rest_pending_action: int | None = None
        self._card_rarity_offset = CARD_RARITY_BASE_OFFSET
        self._potion_reward_odds = POTION_REWARD_BASE_ODDS
        self._event_id = 0
        self._act_index = 0
        self._act_name = "overgrowth"
        self._shared_ancients = [ANCIENT_DARV]
        self._current_ancient = ANCIENT_NEOW
        self._weak_encounters = np.zeros(3, dtype=np.int32)
        self._elite_encounters_seq: list[int] = []
        self._boss_encounter_id: int = 0
        self._weak_encounters_used = 0
        self._normal_encounters: list[int] = []
        self._encounter_seq: list[int] = []
        self._normal_encounters_visited: int = 0
        self._elite_encounters_visited: int = 0
        self._transform_selected_deck_idx: int | None = None
        self._map_nodes: dict[tuple[int, int], RunMapNode] = {}
        self._current_map_coord = MAP_START_COORD
        self._map_option_coords: list[tuple[int, int] | None] = [None] * MAP_CHOICES
        self._handle: int | None = None
        self._combat_obs_buf = (ctypes.c_int * native.OBS_SIZE)()
        self._rew_buf = (ctypes.c_float * 1)()
        self.observation_space = spaces.Box(
            low=0,
            high=2**15,
            shape=(RUN_OBS_SIZE,),
            dtype=np.int32,
        )
        self.action_space = spaces.Discrete(MAX_ACTIONS)

    def reset(self, *, seed=None, options=None):
        super().reset(seed=seed)
        actual_seed = seed if seed is not None else self._seed
        self._seed = actual_seed
        self._run_rng_set = RunRngSet(str(actual_seed))
        self._player_rng = PlayerRngSet(self._run_rng_set)
        self._rng = np.random.default_rng(self._run_rng_set.seed)
        self._elapsed_steps = 0
        self._floor = 1
        self._act_index = 0
        self._act_name = "overgrowth"
        self._shared_ancients = [ANCIENT_DARV]
        self._current_ancient = ANCIENT_NEOW
        self._map_rng = self._run_rng_set.act_map_rng(act_index=0)
        self._deck = list(STARTER_DECK)
        self._gold = 99
        self._player_hp = 64
        self._player_max_hp = 80
        self._potions = [0, 0, 0]
        self._relics = [RELIC_BURNING_BLOOD]
        self._current_node_type = NODE_NORMAL
        self._pending_relic_reward = False
        self._reward_cards[:] = 0
        self._reward_upgraded[:] = False
        self._shop_cards[:] = 0
        self._shop_relics[:] = 0
        self._shop_potions[:] = 0
        self._shop_costs[:] = 0
        self._map_node_types[:] = 0
        self._map_choices[:] = 0
        self._relic_reward = 0
        self._neow_options[:] = 0
        self._silver_crucible_card_rewards_seen = 0
        self._silver_crucible_treasure_seen = 0
        self._fishing_rod_combats_seen = 0
        self._venerable_tea_set_active = False
        self._winged_boots_times_used = 0
        self._shop_removals_used = 0
        self._niche_calls_consumed = 0
        self._rest_pending_action = None
        self._card_rarity_offset = CARD_RARITY_BASE_OFFSET
        self._potion_reward_odds = POTION_REWARD_BASE_ODDS
        self._event_id = 0
        self._weak_encounters_used = 0
        self._normal_encounters = []
        self._elite_encounters_seq = []
        self._boss_encounter_id = 0
        self._encounter_seq = []
        self._normal_encounters_visited = 0
        self._elite_encounters_visited = 0
        self._transform_selected_deck_idx = None
        self._map_nodes = {}
        self._current_map_coord = MAP_START_COORD
        self._map_option_coords = [None] * MAP_CHOICES
        if self._handle is not None:
            native.destroy(self._handle)
            self._handle = None
        for i in range(native.OBS_SIZE):
            self._combat_obs_buf[i] = 0
        self._select_act_and_weak_encounters()
        self._generate_act_map()
        self._enter_ancient_phase()
        return self._obs(), self._info()

    def step(self, action: int):
        self._elapsed_steps += 1

        if self._phase == PHASE_CARD_REWARD:
            return self._step_reward(action)
        if self._phase == PHASE_MAP:
            return self._step_map(action)
        if self._phase == PHASE_REST:
            return self._step_rest(action)
        if self._phase == PHASE_SHOP:
            return self._step_shop(action)
        if self._phase == PHASE_RELIC_REWARD:
            return self._step_relic_reward(action)
        if self._phase == PHASE_EVENT:
            return self._step_event(action)
        if self._phase == PHASE_ANCIENT:
            return self._step_ancient(action)
        if self._phase == PHASE_TRANSFORM_SELECT:
            return self._step_transform_select(action)
        if self._phase == PHASE_COMPLETE:
            return self._obs(), 0.0, True, False, self._info()

        assert self._handle is not None, "Call reset() before step()"
        terminal = native.step(
            self._handle, action, self._combat_obs_buf, self._rew_buf
        )
        reward = float(self._rew_buf[0])
        self._sync_run_state_from_combat_obs()
        truncated = not terminal and self._elapsed_steps >= self._max_episode_steps

        if terminal and native.player_won(self._handle):
            self._after_combat_win()
            return self._obs(), reward, False, truncated, self._info()

        if terminal:
            self._player_hp = 0
        return self._obs(), reward, terminal, truncated, self._info()

    def action_masks(self) -> np.ndarray:
        mask = np.zeros(MAX_ACTIONS, dtype=bool)
        if self._phase == PHASE_CARD_REWARD:
            mask[: REWARD_SKIP_ACTION + 1] = True
            return mask

        if self._phase == PHASE_MAP:
            mask[:MAP_CHOICES] = self._map_node_types != NODE_NONE
            return mask

        if self._phase == PHASE_REST:
            mask[REST_HEAL_ACTION] = True
            mask[REST_UPGRADE_ACTION] = any(
                self._is_upgradable(card) for card in self._deck
            )
            mask[REWARD_SKIP_ACTION] = True  # proceed / skip
            return mask

        if self._phase == PHASE_SHOP:
            for i, card_id in enumerate(self._shop_cards):
                mask[i] = card_id != 0 and self._gold >= int(self._shop_costs[i])
            for action in SHOP_RELIC_ACTIONS:
                index = action - SHOP_RELIC_ACTIONS.start
                mask[action] = self._shop_relics[index] != 0 and self._gold >= int(
                    self._shop_costs[action]
                )
            for action in SHOP_POTION_ACTIONS:
                index = action - SHOP_POTION_ACTIONS.start
                mask[action] = (
                    self._shop_potions[index] != 0
                    and self._gold >= int(self._shop_costs[action])
                    and any(potion == 0 for potion in self._potions)
                )
            mask[SHOP_REMOVE_ACTION] = (
                self._gold >= self._shop_removal_cost() and len(self._deck) > 1
            )
            mask[SHOP_SKIP_ACTION] = True
            return mask

        if self._phase == PHASE_RELIC_REWARD:
            mask[0] = self._relic_reward != 0
            mask[REWARD_SKIP_ACTION] = True  # proceed to next act after boss relic
            return mask

        if self._phase == PHASE_EVENT:
            mask[EVENT_SKIP_ACTION] = True
            if self._event_id == EVENT_UNREST_SITE:
                mask[0] = self._player_hp < self._player_max_hp
                mask[1] = self._player_max_hp > 8
            elif self._event_id == EVENT_AROMA_OF_CHAOS:
                mask[0] = len(self._deck) > 0
                mask[1] = any(self._is_upgradable(card) for card in self._deck)
            elif self._event_id == EVENT_JUNGLE_MAZE_ADVENTURE:
                mask[0] = self._player_hp > 18
                mask[1] = True
            elif self._event_id == EVENT_MORPHIC_GROVE:
                mask[0] = self._gold > 0 and len(self._deck) >= 2
                mask[1] = True
            elif self._event_id == EVENT_BRAIN_LEECH:
                mask[0] = True
                mask[1] = self._player_hp > 5
            elif self._event_id == EVENT_THE_LEGENDS_WERE_TRUE:
                mask[0] = True
                mask[1] = self._player_hp > 8 and any(
                    potion == 0 for potion in self._potions
                )
            elif self._event_id == EVENT_DOORS_OF_LIGHT_AND_DARK:
                mask[0] = any(self._is_upgradable(c) for c in self._deck)
                mask[1] = len(self._deck) > 0
            elif self._event_id == EVENT_SUNKEN_TREASURY:
                mask[0] = True
                mask[1] = True
            elif self._event_id == EVENT_RESULT_PENDING:
                mask[0] = True  # confirm/proceed action
            else:
                mask[: EVENT_SKIP_ACTION + 1] = True
            return mask

        if self._phase == PHASE_ANCIENT:
            mask[: len(self._neow_options)] = self._neow_options != 0
            return mask

        if self._phase == PHASE_TRANSFORM_SELECT:
            # All deck card indices are valid selection targets.
            mask[: len(self._deck)] = True
            return mask

        if self._phase == PHASE_COMPLETE:
            return mask

        assert self._handle is not None, "Call reset() before action_masks()"
        mask_buf = native.valid_actions(self._handle, MAX_ACTIONS)
        return np.array(mask_buf, dtype=bool)

    def close(self):
        if self._handle is not None:
            native.destroy(self._handle)
            self._handle = None

    def _invalid_action(self):
        return self._obs(), -1.0, False, False, self._info()

    def _step_reward(self, action: int):
        if 0 <= action < len(self._reward_cards):
            card_id = int(self._reward_cards[action])
            self._deck.append(-card_id if self._reward_upgraded[action] else card_id)
        elif action != REWARD_SKIP_ACTION:
            return self._invalid_action()

        self._reward_cards[:] = 0
        self._reward_upgraded[:] = False
        if self._pending_relic_reward:
            self._pending_relic_reward = False
            self._enter_relic_reward_phase()
            return self._obs(), 0.0, False, False, self._info()

        return self._advance_after_node()

    def _step_map(self, action: int):
        if not 0 <= action < MAP_CHOICES or self._map_node_types[action] == NODE_NONE:
            return self._invalid_action()

        self._current_node_type = int(self._map_node_types[action])
        encounter_id = int(self._map_choices[action])
        option_coord = self._map_option_coords[action]
        previous_coord = self._current_map_coord
        if option_coord is not None:
            self._current_map_coord = option_coord
            previous = self._map_nodes[previous_coord]
            if option_coord not in (previous.children or set()):
                self._winged_boots_times_used += 1
        self._map_node_types[:] = 0
        self._map_choices[:] = 0
        self._map_option_coords = [None] * MAP_CHOICES
        self._floor += 1

        if self._current_node_type in (NODE_NORMAL, NODE_ELITE, NODE_BOSS):
            # Advance encounter counters matching RoomSet.MarkVisited.
            if self._current_node_type == NODE_NORMAL:
                self._normal_encounters_visited += 1
            elif self._current_node_type == NODE_ELITE:
                self._elite_encounters_visited += 1
            self._phase = PHASE_COMBAT
            self._reset_combat(self._combat_seed(), encounter_id)
            return self._obs(), 0.0, False, False, self._info()

        if self._current_node_type == NODE_REST:
            self._phase = PHASE_REST
            return self._obs(), 0.0, False, False, self._info()

        if self._current_node_type == NODE_SHOP:
            self._enter_shop_phase()
            return self._obs(), 0.0, False, False, self._info()

        if self._current_node_type == NODE_RELIC:
            if (
                RELIC_SILVER_CRUCIBLE in self._relics
                and self._silver_crucible_treasure_seen == 0
            ):
                self._silver_crucible_treasure_seen += 1
                return self._advance_after_node()
            self._enter_relic_reward_phase()
            return self._obs(), 0.0, False, False, self._info()

        if self._current_node_type == NODE_EVENT:
            self._enter_event_phase()
            return self._obs(), 0.0, False, False, self._info()

        if self._current_node_type == NODE_BOSS:
            # Special case for Ancient starting node of each act.
            pass  # should not happen if _enter_ancient_phase is called correctly

        raise ValueError(f"Unsupported map node type: {self._current_node_type}")

    def _step_rest(self, action: int):
        # STS2 rest site uses a two-click confirmation flow:
        #   1st click (choose_rest_option X): select option → show confirmation, no effect
        #   2nd click (choose_rest_option X again): apply effect → show result (can_proceed)
        #   proceed (REST_SKIP_ACTION=3): exit rest site to map
        if action == REWARD_SKIP_ACTION:
            # Proceed / skip action: exit rest site.
            self._rest_pending_action = None
            if RELIC_VENERABLE_TEA_SET in self._relics:
                self._venerable_tea_set_active = True
            return self._advance_after_node()
        if action not in (REST_HEAL_ACTION, REST_UPGRADE_ACTION):
            return self._invalid_action()
        if self._rest_pending_action is None:
            # First click: select option but don't apply yet (stay in rest site).
            self._rest_pending_action = action
            return self._obs(), 0.0, False, False, self._info()
        # Second click (confirmation): apply the effect and show result.
        self._rest_pending_action = None
        if action == REST_HEAL_ACTION:
            heal = max(1, int(self._player_max_hp * 0.3))
            self._player_hp = min(self._player_max_hp, self._player_hp + heal)
            if RELIC_STONE_HUMIDIFIER in self._relics:
                self._player_max_hp += 5
        elif action == REST_UPGRADE_ACTION:
            upgraded = self._upgrade_first_card()
            if not upgraded:
                return self._invalid_action()
        # Stay in PHASE_REST (showing result) until proceed exits.
        return self._obs(), 0.0, False, False, self._info()

    def _step_shop(self, action: int):
        if action in SHOP_CARD_ACTIONS:
            card_id = int(self._shop_cards[action])
            cost = int(self._shop_costs[action])
            if card_id == 0 or self._gold < cost:
                return self._invalid_action()
            self._gold -= cost
            self._deck.append(card_id)
            self._shop_cards[action] = 0
        elif action in SHOP_RELIC_ACTIONS:
            index = action - SHOP_RELIC_ACTIONS.start
            relic_id = int(self._shop_relics[index])
            cost = int(self._shop_costs[action])
            if relic_id == 0 or self._gold < cost:
                return self._invalid_action()
            self._gold -= cost
            self._relics.append(relic_id)
            self._shop_relics[index] = 0
        elif action in SHOP_POTION_ACTIONS:
            index = action - SHOP_POTION_ACTIONS.start
            potion_id = int(self._shop_potions[index])
            cost = int(self._shop_costs[action])
            if potion_id == 0 or self._gold < cost or not self._add_potion(potion_id):
                return self._invalid_action()
            self._gold -= cost
            self._shop_potions[index] = 0
        elif action == SHOP_REMOVE_ACTION:
            if self._gold < self._shop_removal_cost() or len(self._deck) <= 1:
                return self._invalid_action()
            self._gold -= self._shop_removal_cost()
            self._remove_lowest_priority_card()
            self._shop_removals_used += 1
        elif action != SHOP_SKIP_ACTION:
            return self._invalid_action()

        return self._advance_after_node()

    def _step_relic_reward(self, action: int):
        if action == REWARD_SKIP_ACTION:
            if self._current_node_type == NODE_BOSS:
                return self._enter_next_act()
            return self._advance_after_node()

        if action != 0 or self._relic_reward == 0:
            return self._invalid_action()
        self._obtain_relic(int(self._relic_reward))
        self._relic_reward = 0
        if self._current_node_type == NODE_BOSS:
            return self._enter_next_act()
        return self._advance_after_node()

    def _step_event(self, action: int):
        if self._event_id == EVENT_RESULT_PENDING:
            # Any action confirms the result page and exits the event.
            self._event_id = 0
            return self._advance_after_node()
        if self._event_id == EVENT_UNREST_SITE:
            if action == 0:
                self._player_hp = self._player_max_hp
                self._deck.append(POOR_SLEEP_CARD)
            elif action == 1:
                self._player_max_hp = max(1, self._player_max_hp - 8)
                self._player_hp = min(self._player_hp, self._player_max_hp)
                self._relics.append(self._next_relic())
            elif action != EVENT_SKIP_ACTION:
                return self._invalid_action()
        elif self._event_id == EVENT_AROMA_OF_CHAOS:
            if action == 0:
                self._transform_first_card()
            elif action == 1:
                upgraded = self._upgrade_first_card()
                if not upgraded:
                    return self._invalid_action()
            elif action != EVENT_SKIP_ACTION:
                return self._invalid_action()
        elif self._event_id == EVENT_JUNGLE_MAZE_ADVENTURE:
            if action == 0:
                self._player_hp = max(0, self._player_hp - 18)
                self._gold += self._event_gold_amount(150)
            elif action == 1:
                self._gold += self._event_gold_amount(50)
            elif action != EVENT_SKIP_ACTION:
                return self._invalid_action()
        elif self._event_id == EVENT_MORPHIC_GROVE:
            if action == 0:
                self._gold = 0
                self._transform_first_card()
                self._transform_first_card()
            elif action == 1:
                self._gain_max_hp(5)
            elif action != EVENT_SKIP_ACTION:
                return self._invalid_action()
        elif self._event_id == EVENT_DOORS_OF_LIGHT_AND_DARK:
            # Light option (0): upgrade 2 upgradable cards (Niche RNG), then show result page.
            # Dark option (1): remove 1 card from deck (complex, treated as skip to result).
            if action == 0:
                upgradable = [
                    i for i, c in enumerate(self._deck) if self._is_upgradable(c)
                ]
                if upgradable:
                    sorted_idxs = sorted(upgradable, key=lambda i: abs(self._deck[i]))
                    from sts2_gym.game_rng import DotNetRandom, _int32, _uint32

                    niche_seed = _int32(_uint32(self._run_rng_set.seed + _NICHE_HASH))
                    niche_rng_impl = DotNetRandom(niche_seed)
                    for _ in range(self._niche_calls_consumed):
                        niche_rng_impl._sample()
                    n = len(sorted_idxs)
                    for i in range(n - 1, 0, -1):
                        j = niche_rng_impl.next_int(i + 1)
                        sorted_idxs[i], sorted_idxs[j] = sorted_idxs[j], sorted_idxs[i]
                        self._niche_calls_consumed += 1
                    for k in range(min(2, len(sorted_idxs))):
                        deck_idx = sorted_idxs[k]
                        if self._deck[deck_idx] > 0:
                            self._deck[deck_idx] = -self._deck[deck_idx]
                # Transition to result-pending state: player must confirm before leaving.
                self._event_id = EVENT_RESULT_PENDING
                return self._obs(), 0.0, False, False, self._info()
            elif action != EVENT_SKIP_ACTION:
                return self._invalid_action()
        elif self._event_id == EVENT_SUNKEN_TREASURY:
            if action == 0:
                self._gold += 60 + int(self._rng.integers(-8, 9))
            elif action == 1:
                self._gold += 333 + int(self._rng.integers(-30, 31))
            elif action != EVENT_SKIP_ACTION:
                return self._invalid_action()
        elif self._event_id == EVENT_BRAIN_LEECH:
            if action == 0:
                self._deck.append(int(self._rng.choice(IRONCLAD_REWARD_POOL)))
            elif action == 1:
                self._player_hp = max(0, self._player_hp - 5)
                self._event_id = 0
                self._enter_reward_phase()
                return self._obs(), 0.0, False, False, self._info()
            elif action != EVENT_SKIP_ACTION:
                return self._invalid_action()
        elif self._event_id == EVENT_THE_LEGENDS_WERE_TRUE:
            if action == 0:
                self._deck.append(SPOILS_MAP_CARD)
            elif action == 1:
                if self._player_hp <= 8 or not any(
                    potion == 0 for potion in self._potions
                ):
                    return self._invalid_action()
                self._player_hp = max(0, self._player_hp - 8)
                self._add_potion(self._next_potion())
            elif action != EVENT_SKIP_ACTION:
                return self._invalid_action()
        elif action == 0:
            self._gold += 50
            self._add_potion(1)
        elif action == 1:
            if self._player_hp >= self._player_max_hp:
                return self._invalid_action()
            self._player_hp = min(self._player_max_hp, self._player_hp + 15)
        elif action == 2:
            self._deck.append(int(self._rng.choice(IRONCLAD_REWARD_POOL)))
        elif action != EVENT_SKIP_ACTION:
            return self._invalid_action()

        self._event_id = 0
        return self._advance_after_node()

    def _step_ancient(self, action: int):
        if not 0 <= action < len(self._neow_options):
            return self._invalid_action()
        relic_id = int(self._neow_options[action])
        if relic_id == 0:
            return self._invalid_action()

        self._obtain_relic(relic_id)
        if relic_id == RELIC_NEW_LEAF:
            # New Leaf: show card-selection screen so player can pick which deck card to transform.
            self._transform_selected_deck_idx = None
            self._phase = PHASE_TRANSFORM_SELECT
            return self._obs(), 0.0, False, False, self._info()
        if relic_id == RELIC_LOST_COFFER:
            # Lost Coffer: generate 3 card choices (3 calls each) + potion (2 calls),
            # show as a card reward before entering map. The guaranteed potion also
            # decrements _potion_reward_odds as if PotionRewardOdds.Roll() returned True
            # (matching reference traces where floor-3 roll is skipped because odds = 0.2).
            self._reward_cards[:] = self._generate_card_rewards()
            self._reward_upgraded[:] = False
            # CardReward and PotionReward both use the Rewards RNG.
            # CardReward consumes 9 calls (3 cards * 3 calls each).
            # PotionReward consumes 2 calls (rarity, item).
            self._add_potion(self._next_potion(self._player_rng.rewards))
            self._potion_reward_odds -= POTION_REWARD_STEP
            self._phase = PHASE_CARD_REWARD
            return self._obs(), 0.0, False, False, self._info()

        # Handle Ancient relics that need card selection (Astrolabe, Empty Cage)
        if relic_id == RELIC_ASTROLABE:
            self._transform_selected_deck_idx = -3  # special marker for 3 picks
            self._phase = PHASE_TRANSFORM_SELECT
            return self._obs(), 0.0, False, False, self._info()
        if relic_id == RELIC_EMPTY_CAGE:
            self._transform_selected_deck_idx = -2  # special marker for 2 picks
            self._phase = PHASE_TRANSFORM_SELECT
            return self._obs(), 0.0, False, False, self._info()

        advance = _NEOW_REWARDS_RNG_ADVANCES.get(relic_id, 0)
        for _ in range(advance):
            self._player_rng.rewards.next_double()
        self._phase = PHASE_COMBAT  # Dummy phase to trigger map entrance
        self._enter_map_phase()
        return self._obs(), 0.0, False, False, self._info()

    def _step_transform_select(self, action: int):
        """Handle card selection and confirmation for transform-on-pickup relics (e.g. New Leaf).

        Action encoding:
          0 .. len(deck)-1  → select the card at that deck index for transformation
          REWARD_SKIP_ACTION (3) → confirm: apply the transformation and return to Neow/map
          Any other action when a card is already selected → confirm the selection
        """
        if (
            self._transform_selected_deck_idx is not None
            and self._transform_selected_deck_idx < 0
        ):
            # Multi-pick selection (Astrolabe, Empty Cage)
            # Simplification: just apply the effect immediately to the first N cards.
            count = abs(self._transform_selected_deck_idx)
            relic_id = int(self._relics[-1])
            if relic_id == RELIC_ASTROLABE:
                for _ in range(count):
                    self._transform_first_card()
                    # upgrade the transformed card
                    self._deck[-1] = -abs(self._deck[-1])
            elif relic_id == RELIC_EMPTY_CAGE:
                for _ in range(count):
                    self._remove_lowest_priority_card()
            self._transform_selected_deck_idx = None
            self._phase = PHASE_COMBAT
            self._enter_map_phase()
            return self._obs(), 0.0, False, False, self._info()

        if self._transform_selected_deck_idx is None:
            # Phase 1: player selects which card to transform.
            idx = max(0, min(action, len(self._deck) - 1))
            self._transform_selected_deck_idx = idx
            return self._obs(), 0.0, False, False, self._info()
        else:
            # Phase 2: player confirms — transform the selected card using Niche RNG.
            deck_idx = self._transform_selected_deck_idx
            if 0 <= deck_idx < len(self._deck):
                original_id = abs(self._deck[deck_idx])
                # Build transformation pool: same character pool cards, different ID,
                # no Basic/Curse/Status/Ancient rarities.
                transform_pool = [
                    int(c)
                    for c in IRONCLAD_REWARD_POOL
                    if int(c) != original_id
                    and CARD_RARITY_BY_ID.get(int(c), CARD_RARITY_COMMON)
                    in (CARD_RARITY_COMMON, CARD_RARITY_UNCOMMON, CARD_RARITY_RARE)
                ]
                if transform_pool:
                    # Use Niche RNG for the transformation pick (RunState.Rng.Niche.NextItem).
                    from sts2_gym.game_rng import DotNetRandom, _int32, _uint32

                    niche_seed = _int32(_uint32(self._run_rng_set.seed + _NICHE_HASH))
                    niche_rng_impl = DotNetRandom(niche_seed)
                    for _ in range(self._niche_calls_consumed):
                        niche_rng_impl._sample()
                    pick_idx = niche_rng_impl.next_int(len(transform_pool))
                    new_card = transform_pool[pick_idx]
                    self._niche_calls_consumed += 1
                    self._deck[deck_idx] = new_card
            self._transform_selected_deck_idx = None
            # Return to Neow phase to handle the second option, or map if no second option.
            self._phase = PHASE_COMBAT
            self._enter_map_phase()
            return self._obs(), 0.0, False, False, self._info()

    def _obtain_relic(self, relic_id: int) -> None:
        self._relics.append(relic_id)
        up_front = self._run_rng_set.up_front
        if relic_id == RELIC_GOLDEN_PEARL:
            self._gold += 150
        elif relic_id == RELIC_NEOWS_TORMENT:
            self._deck.append(NEOWS_FURY_CARD)
        elif relic_id == RELIC_NEOWS_BONES:
            for _ in range(2):
                bonus = int(NEOW_POSITIVE_RELICS[up_front.next_int(len(NEOW_POSITIVE_RELICS))])
                if bonus not in self._relics:
                    self._relics.append(bonus)
            self._deck.append(CURSE_PLACEHOLDER_CARD)
        elif relic_id == RELIC_NUTRITIOUS_OYSTER:
            self._gain_max_hp(11)
        elif relic_id == RELIC_STRAWBERRY:
            self._gain_max_hp(7)
        elif relic_id == RELIC_PEAR:
            self._gain_max_hp(10)
        elif relic_id == RELIC_MANGO:
            self._gain_max_hp(14)
        elif relic_id == RELIC_LEES_WAFFLE:
            self._gain_max_hp(7)
            self._player_hp = self._player_max_hp
        elif relic_id == RELIC_OLD_COIN:
            self._gold += 300
        elif relic_id == RELIC_SMALL_CAPSULE:
            self._obtain_relic(self._next_relic())
        elif relic_id == RELIC_LARGE_CAPSULE:
            for _ in range(2):
                self._obtain_relic(self._next_relic())
            self._deck.extend([472, 131])
        elif relic_id == RELIC_POMANDER:
            self._upgrade_first_card()
        elif relic_id == RELIC_NEOWS_TALISMAN:
            self._upgrade_last_card_matching(472)
            self._upgrade_last_card_matching(131)
        elif relic_id == RELIC_CURSED_PEARL:
            self._deck.append(CURSE_PLACEHOLDER_CARD)
            self._gold += 333
        elif relic_id == RELIC_HEFTY_TABLET:
            self._deck.append(int(IRONCLAD_REWARD_POOL[up_front.next_int(len(IRONCLAD_REWARD_POOL))]))
            self._deck.append(CURSE_PLACEHOLDER_CARD)
        elif relic_id == RELIC_KALEIDOSCOPE:
            # Simplified: just pick 2 random cards from pool using up_front.
            for _ in range(2):
                self._deck.append(int(IRONCLAD_REWARD_POOL[up_front.next_int(len(IRONCLAD_REWARD_POOL))]))
        elif relic_id == RELIC_ARCANE_SCROLL:
            self._deck.append(int(IRONCLAD_REWARD_POOL[up_front.next_int(len(IRONCLAD_REWARD_POOL))]))
        elif relic_id == RELIC_LEAD_PAPERWEIGHT:
            self._deck.append(int(IRONCLAD_REWARD_POOL[up_front.next_int(len(IRONCLAD_REWARD_POOL))]))
        elif relic_id == RELIC_LOST_COFFER:
            pass  # card reward + potion handled in _step_neow via rewards RNG
        elif relic_id == RELIC_NEW_LEAF:
            pass  # card transform handled in _step_neow via PHASE_TRANSFORM_SELECT
        elif relic_id == RELIC_PHIAL_HOLSTER:
            self._add_potion(self._next_potion(self._player_rng.rewards))
            self._add_potion(self._next_potion(self._player_rng.rewards))
        elif relic_id == RELIC_PRECISE_SCISSORS:
            self._remove_lowest_priority_card()
        elif relic_id == RELIC_SCROLL_BOXES:
            for _ in range(3):
                self._deck.append(int(IRONCLAD_REWARD_POOL[up_front.next_int(len(IRONCLAD_REWARD_POOL))]))
        elif relic_id == RELIC_LEAFY_POULTICE:
            self._player_max_hp = max(1, self._player_max_hp - 12)
            self._player_hp = min(self._player_hp, self._player_max_hp)
            self._transform_first_card_matching(472)
            self._transform_first_card_matching(131)
        elif relic_id == RELIC_PRECARIOUS_SHEARS:
            self._remove_lowest_priority_card()
            self._remove_lowest_priority_card()
            self._player_hp = max(0, self._player_hp - 16)
        elif relic_id == RELIC_SILKEN_TRESS:
            self._gold = 0
        elif relic_id == RELIC_PANDORAS_BOX:
            self._transform_all_matching(472)
            self._transform_all_matching(131)
        elif relic_id == RELIC_CALLING_BELL:
            self._deck.append(CURSE_PLACEHOLDER_CARD)
            for _ in range(3):
                self._obtain_relic(self._next_relic())
        elif relic_id == RELIC_DUSTY_TOME:
            self._deck.append(int(IRONCLAD_REWARD_POOL[up_front.next_int(len(IRONCLAD_REWARD_POOL))]))
            self._deck[-1] = -abs(self._deck[-1])  # upgrade it
        elif relic_id == RELIC_PRISMATIC_GEM:
            self._deck.append(int(IRONCLAD_REWARD_POOL[up_front.next_int(len(IRONCLAD_REWARD_POOL))]))

    def _transform_all_matching(self, card_id: int) -> None:
        for i in range(len(self._deck)):
            if abs(self._deck[i]) == card_id:
                # Transformation uses transformations RNG.
                self._deck[i] = int(IRONCLAD_REWARD_POOL[self._player_rng.transformations.next_int(len(IRONCLAD_REWARD_POOL))])

    def _enter_next_act(self):
        self._act_index += 1
        if self._act_index >= 3:
            self._phase = PHASE_COMPLETE
            return self._obs(), 1.0, True, False, self._info()

        # Update Act name and RNG
        # For now, just cycle act names if needed, but we don't have Act 2/3 data yet.
        self._act_name = "overgrowth"  # placeholder
        self._map_rng = self._run_rng_set.act_map_rng(act_index=self._act_index)

        # Distribute shared ancients
        if self._shared_ancients:
            self._current_ancient = self._shared_ancients.pop(0)
        else:
            self._current_ancient = ANCIENT_DARV  # fallback

        self._generate_act_map()
        self._enter_ancient_phase()
        return self._obs(), 0.0, False, False, self._info()

    def _enter_ancient_phase(self):
        self._phase = PHASE_ANCIENT
        if self._current_ancient == ANCIENT_NEOW:
            self._generate_neow_options()
        elif self._current_ancient == ANCIENT_DARV:
            self._generate_darv_options()
        else:
            # Placeholder for other ancients
            self._neow_options[:] = [
                RELIC_PRISMATIC_GEM,
                RELIC_SEA_GLASS,
                RELIC_DRIFTWOOD,
            ]

    def _generate_darv_options(self) -> None:
        """STS1-style boss relic choices from Darv."""
        # Based on Darv.cs logic: pick 3 random boss relics.
        # Filtering by ActIndex is skipped for simplicity as we only have Darv for now.
        pool = [
            RELIC_ASTROLABE,
            RELIC_BLACK_STAR,
            RELIC_CALLING_BELL,
            RELIC_EMPTY_CAGE,
            RELIC_PANDORAS_BOX,
            RELIC_RUNIC_PYRAMID,
            RELIC_SNECKO_EYE,
            RELIC_ECTOPLASM,
            RELIC_SOZU,
            RELIC_PHILOSOPHERS_STONE,
            RELIC_VELVET_CHOKER,
        ]
        available = [r for r in pool if r not in self._relics]
        if not available:
            available = [RELIC_DUSTY_TOME] * 3
        choices = self._rng.choice(
            available, size=min(3, len(available)), replace=False
        )
        self._neow_options[:] = 0
        for i, c in enumerate(choices):
            self._neow_options[i] = int(c)

    def _after_combat_win(self) -> None:
        # Sync the Python-side shuffle RNG with the native's CountingRandom.
        # The native RNG was advanced by (shufflePreSkip + deckLen-1 + reshuffle_calls).
        # The Python is at (shufflePreSkip + deckLen-1).  Advance it by reshuffle_calls.
        # reshuffle_calls = CountingRandom.CallCount - _run_rng_set.shuffle._rng.call_count.
        if self._run_rng_set is not None and self._handle is not None:
            total_native_calls = native.get_shuffle_rng_call_count(self._handle)
            extra_calls = total_native_calls - self._run_rng_set.shuffle._rng.call_count
            for _ in range(extra_calls):
                self._run_rng_set.shuffle.next_double()
            # NicheHpRng.CallCount = nicheSkipCount + new_hp_calls, so assign directly.
            self._niche_calls_consumed = native.get_niche_rng_call_count(self._handle)

        # Real game order in GenerateRewardsFor + GenerateWithoutOffering:
        #   1. RollForPotionAndAddTo → PotionRewardOdds.Roll() → 1 Rewards RNG call
        #   2. GoldReward.Populate() → 1 Rewards RNG call
        #   3. PotionReward.Populate() (if potion won) → 2 Rewards RNG calls
        #   4. CardReward.Populate() → 3 cards × 3 calls (rarity + selection + upgrade roll) = 9
        potion_val = self._player_rng.rewards.next_double()
        self._gold += self._gold_reward_for_node()
        if RELIC_AMETHYST_AUBERGINE in self._relics and self._current_node_type in (
            NODE_NORMAL,
            NODE_ELITE,
        ):
            self._gold += 15
        potion_won = self._check_potion_roll(potion_val)
        if potion_won:
            self._add_potion(self._next_potion(self._player_rng.rewards))
        if RELIC_BURNING_BLOOD in self._relics:
            self._player_hp = min(self._player_max_hp, self._player_hp + 6)
        if RELIC_BLACK_BLOOD in self._relics:
            self._player_hp = min(self._player_max_hp, self._player_hp + 12)
        if (
            RELIC_MEAT_ON_THE_BONE in self._relics
            and self._player_hp <= self._player_max_hp // 2
        ):
            self._player_hp = min(self._player_max_hp, self._player_hp + 12)
        if RELIC_FISHING_ROD in self._relics and self._current_node_type == NODE_NORMAL:
            self._fishing_rod_combats_seen += 1
            if self._fishing_rod_combats_seen % 3 == 0:
                self._upgrade_random_card()
        if RELIC_WAR_HAMMER in self._relics and self._current_node_type == NODE_ELITE:
            for _ in range(4):
                if not self._upgrade_random_card():
                    break
        self._pending_relic_reward = self._current_node_type in (NODE_ELITE, NODE_BOSS)
        self._enter_reward_phase()

    def _advance_after_node(self):
        if self._floor >= self._max_floors:
            self._phase = PHASE_COMPLETE
            return self._obs(), 0.0, True, False, self._info()

        self._enter_map_phase()
        return self._obs(), 0.0, False, False, self._info()

    def _enter_reward_phase(self):
        self._phase = PHASE_CARD_REWARD
        self._reward_cards[:] = self._generate_card_rewards()
        self._reward_upgraded[:] = False
        if (
            RELIC_SILVER_CRUCIBLE in self._relics
            and self._silver_crucible_card_rewards_seen < 3
        ):
            self._reward_upgraded[:] = True
            self._silver_crucible_card_rewards_seen += 1

    def _enter_map_phase(self):
        self._phase = PHASE_MAP
        self._map_node_types[:] = 0
        self._map_choices[:] = 0
        self._map_option_coords = [None] * MAP_CHOICES
        current = self._map_nodes[self._current_map_coord]
        children = sorted(
            current.children or set(), key=lambda coord: (coord[1], coord[0])
        )
        options = children
        if RELIC_WINGED_BOOTS in self._relics and self._winged_boots_times_used < 3:
            next_row = current.row + 1
            winged_coords = sorted(
                [
                    coord
                    for coord, node in self._map_nodes.items()
                    if node.row == next_row and coord not in options
                ],
                key=lambda coord: (coord[1], coord[0]),
            )
            options = [*options, *winged_coords]
        for i, coord in enumerate(options[:MAP_CHOICES]):
            node = self._map_nodes[coord]
            node_type = MAP_NODE_TO_OBS[node.node_type]
            self._map_node_types[i] = node_type
            if node_type == NODE_NORMAL and self._encounter_seq:
                # Use the next encounter in sequence (matching RoomSet.NextNormalEncounter),
                # but don't advance the counter until the player actually enters the room.
                enc = self._encounter_seq[
                    self._normal_encounters_visited % len(self._encounter_seq)
                ]
                self._map_choices[i] = int(enc)
            elif node_type == NODE_ELITE and self._elite_encounters_seq:
                enc = self._elite_encounters_seq[
                    self._elite_encounters_visited % len(self._elite_encounters_seq)
                ]
                self._map_choices[i] = int(enc)
            elif node_type == NODE_BOSS:
                self._map_choices[i] = self._boss_encounter_id
            else:
                self._map_choices[i] = (
                    node.encounter_id
                    if node.encounter_id
                    else self._encounter_for_node(node_type)
                )
            self._map_option_coords[i] = coord

    def _enter_shop_phase(self):
        self._phase = PHASE_SHOP
        # MerchantInventory.cs: list.NextItem(Player.PlayerRng.Shops).SetOnSale()
        # NextItem calls NextInt(count).
        sale_index = self._player_rng.shops.next_int(5)
        self._shop_cards[:] = self._generate_shop_cards()
        self._shop_relics[:] = [self._next_relic() for _ in range(3)]
        self._shop_potions[:] = self._next_potions(3, self._player_rng.shops)
        self._shop_costs[:] = 0
        for action, card_id in enumerate(self._shop_cards):
            cost = self._shop_card_cost(int(card_id), colorless=action >= 5)
            if action == sale_index:
                cost //= 2
            self._shop_costs[action] = cost
        for action in SHOP_RELIC_ACTIONS:
            index = action - SHOP_RELIC_ACTIONS.start
            self._shop_costs[action] = self._shop_relic_cost(
                int(self._shop_relics[index])
            )
        for action in SHOP_POTION_ACTIONS:
            index = action - SHOP_POTION_ACTIONS.start
            self._shop_costs[action] = self._shop_potion_cost(
                int(self._shop_potions[index])
            )
        self._shop_costs[SHOP_REMOVE_ACTION] = self._shop_removal_cost()

    def _enter_relic_reward_phase(self):
        self._phase = PHASE_RELIC_REWARD
        self._relic_reward = self._next_relic()

    def _enter_event_phase(self):
        self._phase = PHASE_EVENT
        event_pool = [
            EVENT_JUNGLE_MAZE_ADVENTURE,
            EVENT_BRAIN_LEECH,
            EVENT_DOORS_OF_LIGHT_AND_DARK,
            EVENT_SUNKEN_TREASURY,
        ]
        if self._player_hp >= 10 and len(self._combat_deck()) > 0:
            event_pool.append(EVENT_THE_LEGENDS_WERE_TRUE)
        if self._gold >= 100 and len(self._deck) >= 2:
            event_pool.append(EVENT_MORPHIC_GROVE)
        if self._player_hp <= int(self._player_max_hp * 0.7):
            event_pool.extend(
                [EVENT_UNREST_SITE, EVENT_AROMA_OF_CHAOS, EVENT_SIMPLE_REWARD]
            )
        else:
            event_pool.extend([EVENT_AROMA_OF_CHAOS, EVENT_SIMPLE_REWARD])
        # Use UpFront RNG for event selection.
        self._event_id = int(event_pool[self._run_rng_set.up_front.next_int(len(event_pool))])

    def _select_act_and_weak_encounters(self):
        # Act selection: separate Rng seeded from uint(run_seed), matches
        # BeginRunLocally's local rng used for ActModel.GetRandomList.
        act_rng = DotNetRandom(_int32(self._run_rng_set.seed))
        # ActModel.GetRandomList: selects Underdocks when next_bool() is true
        # (decompiled: if flag && (flag2 || rng.NextBool()) → Underdocks).
        # next_bool() returns True when next_int(2)==0; game picks Underdocks when True.
        use_underdocks = act_rng.next_bool()
        if use_underdocks:
            self._act = "underdocks"
            weak_pool = list(_UNDERDOCKS_WEAK_POOL)
            event_shuffle_calls = _UNDERDOCKS_EVENT_SHUFFLE_CALLS
        else:
            self._act = "overgrowth"
            weak_pool = list(_OVERGROWTH_WEAK_POOL)
            event_shuffle_calls = _OVERGROWTH_EVENT_SHUFFLE_CALLS

        # Fast-forward UpFront RNG through pre-calls, then event shuffle, then grab.
        up_front = self._run_rng_set.up_front
        for _ in range(_UPFRONT_PRE_CALLS):
            up_front.next_double()
        # Event list UnstableShuffle: event_shuffle_calls = N-1 for N events.
        for _ in range(event_shuffle_calls):
            up_front.next_int(event_shuffle_calls + 1)
        # GrabBag.GrabAndRemove for 3 weak encounter slots.
        encounters = []
        remaining = list(weak_pool)
        for _ in range(3):
            d = up_front.next_double()
            idx = int(d * len(remaining))
            encounters.append(remaining[idx])
            remaining.pop(idx)
        self._weak_encounters[:] = encounters

        # GrabBag.GrabAndRemove for NORMAL_ENCOUNTER_SLOTS (=12) regular encounter slots,
        # matching ActModel.GenerateRooms' grabBag2 loop (AddWithoutRepeatingTags, 1 rng
        # call each for the typical case where the predicate is satisfied on the first try).
        normal_pool = list(
            UNDERDOCKS_NORMAL_ENCOUNTERS
            if use_underdocks
            else OVERGROWTH_NORMAL_ENCOUNTERS
        )
        bag: list[int] = []
        normal_list: list[int] = []
        for _ in range(_NORMAL_ENCOUNTER_SLOTS):
            if not bag:
                bag = list(normal_pool)
            d = up_front.next_double()
            idx = int(d * len(bag))
            enc = bag[idx]
            bag.pop(idx)
            normal_list.append(enc)
        self._normal_encounters = normal_list

        # GrabBag.GrabAndRemove for _ELITE_ENCOUNTER_SLOTS (=15) elite encounter slots,
        # matching ActModel.GenerateRooms' grabBag3 loop.
        elite_pool = list(
            UNDERDOCKS_ELITE_ENCOUNTERS
            if use_underdocks
            else OVERGROWTH_ELITE_ENCOUNTERS
        )
        elite_bag: list[int] = []
        elite_list: list[int] = []
        for _ in range(_ELITE_ENCOUNTER_SLOTS):
            if not elite_bag:
                elite_bag = list(elite_pool)
            d = up_front.next_double()
            idx = int(d * len(elite_bag))
            enc = elite_bag[idx]
            elite_bag.pop(idx)
            elite_list.append(enc)
        self._elite_encounters_seq = elite_list

        # Boss: rng.NextItem(AllBossEncounters) = 1 up_front call.
        boss_pool = list(
            UNDERDOCKS_BOSS_ENCOUNTERS if use_underdocks else OVERGROWTH_BOSS_ENCOUNTERS
        )
        d = up_front.next_double()
        self._boss_encounter_id = boss_pool[int(d * len(boss_pool))]

        # Build the unified normal-encounter sequence matching RoomSet.normalEncounters:
        # first 3 are weak encounters, remaining 12 are regular encounters. The counter
        # _normal_encounters_visited advances only when the player enters a Monster room,
        # matching RoomSet.MarkVisited(RoomType.Monster).
        self._encounter_seq = list(self._weak_encounters) + self._normal_encounters

    def _generate_act_map(self) -> None:
        self._map_nodes = {}
        self._current_map_coord = MAP_START_COORD
        self._map_option_coords = [None] * MAP_CHOICES
        self._get_or_create_map_node(*MAP_START_COORD).node_type = "Ancient"
        self._get_or_create_map_node(*MAP_BOSS_COORD).node_type = "Boss"

        # C# StandardActMap constructor calls actModel.GetMapPointTypes(mapRng) once,
        # before GenerateMap. Store results here for type assignment after paths.
        rest_count = self._map_rng.next_gaussian_int(7, 1, 6, 7)
        unknown_count = self._map_rng.next_gaussian_int(12, 1, 10, 14)

        start_points: list[tuple[int, int]] = []
        for path_index in range(MAP_PATH_ITERATIONS):
            start = self._get_or_create_map_node(self._map_rng.next_int(MAP_WIDTH), 1)
            if path_index == 1:
                while (start.col, start.row) in start_points:
                    start = self._get_or_create_map_node(
                        self._map_rng.next_int(MAP_WIDTH), 1
                    )
            if (start.col, start.row) not in start_points:
                start_points.append((start.col, start.row))
            self._generate_map_path(start)

        for coord in start_points:
            self._add_map_edge(MAP_START_COORD, coord)
        for coord, node in list(self._map_nodes.items()):
            if node.row == MAP_BOSS_ROW - 1:
                self._add_map_edge(coord, MAP_BOSS_COORD)

        self._assign_map_point_types(rest_count, unknown_count)
        self._prune_and_repair(rest_count, unknown_count)
        self._center_grid()
        self._spread_adjacent_map_points()
        self._straighten_paths()
        self._assign_encounter_ids()

    _MAP_TYPE_IDS = {
        "Unassigned": 0,
        "Unknown": 1,
        "Shop": 2,
        "Treasure": 3,
        "RestSite": 4,
        "Monster": 5,
        "Elite": 6,
        "Boss": 7,
        "Ancient": 8,
    }

    def _prune_and_repair(self, rest_count: int, unknown_count: int) -> None:
        """Faithful implementation of MapPathPruning.PruneAndRepair."""
        for _ in range(3):
            self._prune_duplicate_segments()
            if not self._repair_pruned_point_types(rest_count, unknown_count):
                break

    def _repair_pruned_point_types(self, rest_count: int, unknown_count: int) -> bool:
        """Faithful implementation of MapPathPruning.RepairPrunedPointTypes."""
        any_repaired = False
        # NumOfElites is 8 for high ascension.
        any_repaired |= self._repair_point_type("Shop", 3)
        any_repaired |= self._repair_point_type("Elite", 8)
        any_repaired |= self._repair_point_type("RestSite", rest_count)
        any_repaired |= self._repair_point_type("Unknown", unknown_count)
        return any_repaired

    def _repair_point_type(self, node_type: str, target_count: int) -> bool:
        """Faithful implementation of MapPathPruning.RepairPointType."""
        current_count = sum(
            1 for n in self._map_nodes.values() if n.node_type == node_type
        )
        needed = target_count - current_count
        if needed <= 0:
            return False

        candidates = [
            n
            for n in self._map_nodes.values()
            if n.node_type == "Monster" and n.can_be_modified
        ]
        self._map_rng.stable_shuffle(candidates, key=lambda n: (n.col, n.row))

        repaired = False
        for node in candidates:
            if needed == 0:
                break
            if self._is_valid_map_point_type(node_type, node):
                node.node_type = node_type
                needed -= 1
                repaired = True
        return repaired

    def _prune_duplicate_segments(self) -> None:
        """Faithful implementation of MapPathPruning.PruneDuplicateSegments."""
        num = 0
        while num < 50:
            matching_segments = self._find_matching_segments()
            if not self._prune_paths(matching_segments):
                break
            num += 1

    def _find_matching_segments(self) -> list[list[list[tuple[int, int]]]]:
        """Faithful implementation of MapPathPruning.FindMatchingSegments."""
        paths = self._find_all_paths(MAP_START_COORD)
        segments_dict: dict[str, list[list[tuple[int, int]]]] = {}

        for path in paths:
            # AddSegmentsToDictionary
            for i in range(len(path) - 1):
                if not self._is_valid_segment_start(path[i]):
                    continue
                for j in range(2, len(path) - i):
                    end_coord = path[i + j]
                    if self._is_valid_segment_end(end_coord):
                        segment = path[i : i + j + 1]
                        key = self._generate_segment_key(segment)
                        if key not in segments_dict:
                            segments_dict[key] = [segment]
                        elif not self._any_overlapping_segments(
                            segments_dict[key], segment
                        ):
                            segments_dict[key].append(segment)

        # Match SortedDictionary<string, ...>(StringComparer.Ordinal)
        sorted_keys = sorted(segments_dict.keys())
        return [segments_dict[k] for k in sorted_keys if len(segments_dict[k]) > 1]

    def _is_valid_segment_start(self, coord: tuple[int, int]) -> bool:
        if coord[1] == 0:
            return True
        node = self._map_nodes.get(coord)
        return node is not None and len(node.children) > 1

    def _is_valid_segment_end(self, coord: tuple[int, int]) -> bool:
        node = self._map_nodes.get(coord)
        return node is not None and len(node.parents) >= 2

    def _generate_segment_key(self, segment: list[tuple[int, int]]) -> str:
        start = segment[0]
        end = segment[-1]
        if start[1] == 0:
            key = f"{start[1]}-{end[0]},{end[1]}-"
        else:
            key = f"{start[0]},{start[1]}-{end[0]},{end[1]}-"

        types = "".join(
            str(self._MAP_TYPE_IDS.get(self._map_nodes[c].node_type, 0))
            for c in segment
        )
        return key + types

    def _any_overlapping_segments(
        self, existing: list[list[tuple[int, int]]], segment: list[tuple[int, int]]
    ) -> bool:
        return any(self._overlapping_segments(e, segment) for e in existing)

    def _overlapping_segments(
        self, a: list[tuple[int, int]], b: list[tuple[int, int]]
    ) -> bool:
        if len(a) < 3 or len(b) < 3:
            return False
        # MapPathPruning.OverlappingSegment matches on intermediate nodes.
        for i in range(1, len(a) - 1):
            if i < len(b) - 1 and a[i] == b[i]:
                return True
        return False

    def _prune_paths(
        self, matching_segments: list[list[list[tuple[int, int]]]]
    ) -> bool:
        """Faithful implementation of MapPathPruning.PrunePaths."""
        for group in matching_segments:
            self._map_rng.shuffle(group)  # UnstableShuffle
            if self._prune_all_but_last(group) != 0:
                return True
            if self._break_any_parent_child_relationship(group):
                return True
        return False

    def _prune_all_but_last(self, matches: list[list[tuple[int, int]]]) -> int:
        num = 0
        for i, match in enumerate(matches):
            if i == len(matches) - 1:
                return num
            if self._prune_segment(match):
                num += 1
        return num

    def _prune_segment(self, segment: list[tuple[int, int]]) -> bool:
        """Faithful implementation of MapPathPruning.PruneSegment."""
        pruned = False
        for i in range(len(segment) - 1):
            coord = segment[i]
            if not self._is_in_map(coord):
                return True
            node = self._map_nodes.get(coord)
            if node is None:
                continue

            if (
                len(node.children) > 1
                or len(node.parents) > 1
                or any(
                    len(self._map_nodes[p].children) == 1 for p in node.parents
                )  # Simplified Any
            ):
                continue

            source = segment[i:]
            if not any(
                len(self._map_nodes[c].children) > 1
                and len(self._map_nodes[c].parents) == 1
                for c in source
                if c in self._map_nodes
            ):
                last_node = self._map_nodes.get(segment[-1])
                if last_node and len(last_node.parents) == 1:
                    return False
                if not any(
                    len(self._map_nodes[c].parents) == 1
                    for c in node.children
                    if c not in segment
                ):
                    self._remove_map_point(coord)
                    pruned = True
        return pruned

    def _is_in_map(self, coord: tuple[int, int]) -> bool:
        if coord[1] == 0:
            return True  # Ancient is always in map
        if coord == MAP_BOSS_COORD:
            return True
        return coord in self._map_nodes

    def _remove_map_point(self, coord: tuple[int, int]) -> None:
        """Remove a map point, severing all edges without reconnecting parents to children.

        Matches C# MapPathPruning.RemovePoint: removes grid cell, severs child edges FROM
        the node, and removes it FROM its parents. Does NOT reconnect parents to children —
        the duplicate segment keeps the map connected.
        """
        node = self._map_nodes.pop(coord, None)
        if node is None:
            return
        # Sever edges from node to its children (and remove node as parent of each child).
        for c_coord in list(node.children):
            c_node = self._map_nodes.get(c_coord)
            if c_node and coord in c_node.parents:
                c_node.parents.remove(coord)
        # Remove this node from each parent's children list.
        for p_coord in list(node.parents):
            p_node = self._map_nodes.get(p_coord)
            if p_node and coord in p_node.children:
                p_node.children.remove(coord)

    def _break_any_parent_child_relationship(
        self, matches: list[list[tuple[int, int]]]
    ) -> bool:
        for match in matches:
            if self._break_parent_child_relationship_in_segment(match):
                return True
        return False

    def _break_parent_child_relationship_in_segment(
        self, segment: list[tuple[int, int]]
    ) -> bool:
        pruned = False
        for i in range(len(segment) - 1):
            node = self._map_nodes.get(segment[i])
            if node and len(node.children) >= 2:
                child_coord = segment[i + 1]
                child_node = self._map_nodes.get(child_coord)
                if child_node and len(child_node.parents) != 1:
                    if child_coord in node.children:
                        node.children.remove(child_coord)
                    if segment[i] in child_node.parents:
                        child_node.parents.remove(segment[i])
                    pruned = True
        return pruned

    def _center_grid(self) -> None:
        """Faithful implementation of MapPostProcessing.CenterGrid."""
        left_empty = self._is_column_empty(0) and self._is_column_empty(1)
        right_empty = self._is_column_empty(MAP_WIDTH - 1) and self._is_column_empty(
            MAP_WIDTH - 2
        )

        shift = 0
        if left_empty and not right_empty:
            shift = -1
        elif not left_empty and right_empty:
            shift = 1

        if shift == 0:
            return

        new_nodes = {}
        for (col, row), node in self._map_nodes.items():
            if row == 0 or row == MAP_BOSS_COORD[1]:
                new_nodes[(col, row)] = node
                continue

            new_col = col + shift
            node.col = new_col
            new_nodes[(new_col, row)] = node

        # Update all parent/child coordinate references.
        for node in new_nodes.values():
            node.children = [
                (c[0] + shift if 0 < c[1] < MAP_BOSS_COORD[1] else c[0], c[1])
                for c in node.children
            ]
            node.parents = [
                (p[0] + shift if 0 < p[1] < MAP_BOSS_COORD[1] else p[0], p[1])
                for p in node.parents
            ]

        self._map_nodes = new_nodes

    def _spread_adjacent_map_points(self) -> None:
        """Faithful implementation of MapPostProcessing.SpreadAdjacentMapPoints."""
        for row in range(MAP_BOSS_ROW + 1):
            row_nodes = sorted(
                [node for node in self._map_nodes.values() if node.row == row],
                key=lambda n: n.col,
            )
            if not row_nodes:
                continue

            changed = True
            while changed:
                changed = False
                for item in row_nodes:
                    col = item.col
                    allowed = self._get_allowed_positions(item)
                    current_gap = self._compute_gap(col, row_nodes, item)
                    best_col = col
                    best_gap = current_gap

                    for candidate_col in allowed:
                        if candidate_col != col and not any(
                            n.col == candidate_col and n != item for n in row_nodes
                        ):
                            cand_gap = self._compute_gap(candidate_col, row_nodes, item)
                            if cand_gap > best_gap:
                                best_col = candidate_col
                                best_gap = cand_gap

                    if best_col != col:
                        self._move_node(item, best_col, row)
                        changed = True

    def _get_allowed_positions(self, node: RunMapNode) -> set[int]:
        allowed = set(range(MAP_WIDTH))
        for p_coord in node.parents:
            p_allowed = {p_coord[0] - 1, p_coord[0], p_coord[0] + 1}
            allowed &= {c for c in p_allowed if 0 <= c < MAP_WIDTH}
        for c_coord in node.children:
            c_allowed = {c_coord[0] - 1, c_coord[0], c_coord[0] + 1}
            allowed &= {c for c in c_allowed if 0 <= c < MAP_WIDTH}
        return allowed

    def _compute_gap(
        self, candidate_col: int, row_nodes: list[RunMapNode], current_node: RunMapNode
    ) -> int:
        gap = 999999
        for n in row_nodes:
            if n != current_node:
                gap = min(gap, abs(candidate_col - n.col))
        return gap

    def _straighten_paths(self) -> None:
        """Faithful implementation of MapPostProcessing.StraightenPaths."""
        for row in range(MAP_BOSS_ROW + 1):
            for col in range(MAP_WIDTH):
                node = self._map_nodes.get((col, row))
                if not node or len(node.parents) != 1 or len(node.children) != 1:
                    continue
                p_coord = next(iter(node.parents))
                c_coord = next(iter(node.children))
                flag = node.col < c_coord[0] and node.col < p_coord[0]
                flag2 = node.col > c_coord[0] and node.col > p_coord[0]

                if flag and col < MAP_WIDTH - 1:
                    new_col = col + 1
                    if (new_col, row) not in self._map_nodes:
                        self._move_node(node, new_col, row)
                        continue
                if flag2 and col > 0:
                    new_col = col - 1
                    if (new_col, row) not in self._map_nodes:
                        self._move_node(node, new_col, row)

    def _move_node(self, node: RunMapNode, new_col: int, row: int) -> None:
        old_coord = (node.col, row)
        new_coord = (new_col, row)
        node.col = new_col
        del self._map_nodes[old_coord]
        self._map_nodes[new_coord] = node

        for p_coord in node.parents:
            p_node = self._map_nodes.get(p_coord)
            if p_node:
                if old_coord in p_node.children:
                    p_node.children.remove(old_coord)
                if new_coord not in p_node.children:
                    p_node.children.append(new_coord)
        for c_coord in node.children:
            c_node = self._map_nodes.get(c_coord)
            if c_node:
                if old_coord in c_node.parents:
                    c_node.parents.remove(old_coord)
                if new_coord not in c_node.parents:
                    c_node.parents.append(new_coord)

    def _is_column_empty(self, col: int) -> bool:
        for row in range(1, MAP_BOSS_ROW):
            if (col, row) in self._map_nodes:
                return False
        return True

    def _find_all_paths(self, start: tuple[int, int]) -> list[list[tuple[int, int]]]:
        """Find all paths from start to MAP_BOSS_COORD."""
        if start == MAP_BOSS_COORD:
            return [[start]]
        node = self._map_nodes.get(start)
        if node is None:
            return []
        result: list[list[tuple[int, int]]] = []
        for child in node.children:
            for path in self._find_all_paths(child):
                result.append([start] + path)
        if not result:
            result = [[start]]
        return result

    def _get_or_create_map_node(self, col: int, row: int) -> RunMapNode:
        coord = (col, row)
        node = self._map_nodes.get(coord)
        if node is None:
            node = RunMapNode(col, row)
            self._map_nodes[coord] = node
        return node

    def _add_map_edge(
        self, parent_coord: tuple[int, int], child_coord: tuple[int, int]
    ):
        parent = self._get_or_create_map_node(*parent_coord)
        child = self._get_or_create_map_node(*child_coord)
        if child_coord not in parent.children:
            parent.children.append(child_coord)
        if parent_coord not in child.parents:
            child.parents.append(parent_coord)

    def _generate_map_path(self, start: RunMapNode) -> None:
        current = start
        while current.row < MAP_BOSS_ROW - 1:
            child_coord = self._generate_next_map_coord(current)
            self._add_map_edge((current.col, current.row), child_coord)
            current = self._map_nodes[child_coord]

    def _generate_next_map_coord(self, current: RunMapNode) -> tuple[int, int]:
        deltas = [-1, 0, 1]
        self._map_rng.stable_shuffle(deltas)
        for delta in deltas:
            target_col = max(0, min(MAP_WIDTH - 1, current.col + int(delta)))
            if not self._has_invalid_crossover(current, target_col):
                return (target_col, current.row + 1)
        raise RuntimeError(
            f"Cannot find next map node from {(current.col, current.row)}"
        )

    def _has_invalid_crossover(self, current: RunMapNode, target_col: int) -> bool:
        delta = target_col - current.col
        if delta == 0:
            return False
        sibling = self._map_nodes.get((target_col, current.row))
        if sibling is None:
            return False
        for child_col, _ in sibling.children:
            if child_col - sibling.col == -delta:
                return True
        return False

    def _assign_encounter_ids(self) -> None:
        """Assign encounter IDs to all combat nodes.

        Monster nodes are grouped by floor row; all nodes at the same floor
        share the same pre-selected weak encounter from _weak_encounters.
        """
        monster_rows = sorted(
            {n.row for n in self._map_nodes.values() if n.node_type == "Monster"}
        )
        weak_idx = 0
        normal_idx = 0
        row_encounters: dict[int, int] = {}
        for row in monster_rows:
            if weak_idx < len(self._weak_encounters):
                row_encounters[row] = int(self._weak_encounters[weak_idx])
                weak_idx += 1
            elif normal_idx < len(self._normal_encounters):
                row_encounters[row] = self._normal_encounters[normal_idx]
                normal_idx += 1
            else:
                row_encounters[row] = int(
                    self._rng.choice(self._normal_encounter_pool())
                )
        # Assign elite encounters from the pre-computed sequence (up_front GrabBag).
        elite_rows = sorted(
            {n.row for n in self._map_nodes.values() if n.node_type == "Elite"}
        )
        elite_row_encounters: dict[int, int] = {}
        elite_seq_idx = 0
        for row in elite_rows:
            if elite_seq_idx < len(self._elite_encounters_seq):
                elite_row_encounters[row] = self._elite_encounters_seq[elite_seq_idx]
                elite_seq_idx += 1
            else:
                elite_row_encounters[row] = int(
                    self._rng.choice(self._elite_encounter_pool())
                )

        for node in self._map_nodes.values():
            if node.node_type == "Monster":
                node.encounter_id = row_encounters[node.row]
            elif node.node_type == "Elite":
                node.encounter_id = elite_row_encounters.get(
                    node.row, int(self._rng.choice(self._elite_encounter_pool()))
                )
            elif node.node_type == "Boss":
                node.encounter_id = (
                    self._boss_encounter_id
                    if self._boss_encounter_id
                    else int(self._rng.choice(self._boss_encounter_pool()))
                )

    def _assign_map_point_types(self, rest_count: int, unknown_count: int) -> None:
        for node in self._map_nodes.values():
            if node.row == MAP_FINAL_REST_ROW:
                node.node_type = "RestSite"
                node.can_be_modified = False
            elif node.row == MAP_TREASURE_ROW:
                node.node_type = "Treasure"
                node.can_be_modified = False
            elif node.row == 1:
                node.node_type = "Monster"
                node.can_be_modified = False

        type_queue = (
            ["RestSite"] * rest_count
            + ["Shop"] * 3
            + ["Elite"] * 8
            + ["Unknown"] * unknown_count
        )
        candidates = [
            node
            for node in self._map_nodes.values()
            if node.node_type == "Unassigned" and node.row not in (0, MAP_BOSS_ROW)
        ]
        for _ in range(3):
            if not type_queue:
                break
            self._map_rng.stable_shuffle(candidates, key=lambda n: (n.col, n.row))
            for node in candidates:
                if not type_queue or node.node_type != "Unassigned":
                    continue
                node.node_type = self._next_valid_map_point_type(type_queue, node)

        for node in self._map_nodes.values():
            if node.node_type == "Unassigned":
                node.node_type = "Monster"

        self._map_nodes[MAP_START_COORD].node_type = "Ancient"
        self._map_nodes[MAP_BOSS_COORD].node_type = "Boss"

    def _next_valid_map_point_type(
        self, type_queue: list[str], node: RunMapNode
    ) -> str:
        for _ in range(len(type_queue)):
            node_type = type_queue.pop(0)
            if self._is_valid_map_point_type(node_type, node):
                return node_type
            type_queue.append(node_type)
        return "Unassigned"

    def _is_valid_map_point_type(self, node_type: str, node: RunMapNode) -> bool:
        if node.row < 6 and node_type in MAP_LOWER_RESTRICTED:
            return False
        if node.row >= MAP_BOSS_ROW - 2 and node_type in MAP_UPPER_RESTRICTED:
            return False
        if node_type in MAP_ADJACENCY_RESTRICTED:
            adjacent = set(node.parents) | set(node.children)
            if any(self._map_nodes[coord].node_type == node_type for coord in adjacent):
                return False
        if node_type in MAP_SIBLING_RESTRICTED:
            siblings = set()
            for parent_coord in node.parents:
                siblings.update(self._map_nodes[parent_coord].children)
            siblings.discard((node.col, node.row))
            if any(self._map_nodes[coord].node_type == node_type for coord in siblings):
                return False
        return True

    def _normal_encounter_pool(self) -> np.ndarray:
        return (
            OVERGROWTH_NORMAL_ENCOUNTERS
            if self._act == "overgrowth"
            else UNDERDOCKS_NORMAL_ENCOUNTERS
        )

    def _elite_encounter_pool(self) -> np.ndarray:
        return (
            OVERGROWTH_ELITE_ENCOUNTERS
            if self._act == "overgrowth"
            else UNDERDOCKS_ELITE_ENCOUNTERS
        )

    def _boss_encounter_pool(self) -> np.ndarray:
        return (
            OVERGROWTH_BOSS_ENCOUNTERS
            if self._act == "overgrowth"
            else UNDERDOCKS_BOSS_ENCOUNTERS
        )

    def _encounter_for_node(self, node_type: int) -> int:
        if node_type == NODE_ELITE:
            return int(self._rng.choice(self._elite_encounter_pool()))
        if node_type == NODE_BOSS:
            return int(self._rng.choice(self._boss_encounter_pool()))
        return 0

    def _reset_combat(self, seed: int, encounter_id: int | None = None):
        if self._handle is not None:
            native.destroy(self._handle)
        self._handle = native.create(seed)
        if encounter_id is not None and self._current_node_type == NODE_BOSS:
            if RELIC_PANTOGRAPH in self._relics:
                self._player_hp = min(self._player_max_hp, self._player_hp + 25)
        if encounter_id is None:
            native.reset_with_deck(
                self._handle, self._combat_deck(), self._combat_obs_buf
            )
        else:
            relics = self._relics
            if self._venerable_tea_set_active:
                relics = [*relics, RELIC_VENERABLE_TEA_SET_ACTIVE]
                self._venerable_tea_set_active = False
            deck = self._combat_deck()
            encounter_rng_seed = self._encounter_rng_seed(encounter_id)
            if self._run_rng_set is not None:
                # Read call_count before the pre-shuffle to compute the skip.
                shuffle_pre_skip = self._run_rng_set.shuffle._rng.call_count
                self._run_rng_set.shuffle.shuffle(deck)
                shuffle_rng_seed = _int32(
                    _uint32(self._run_rng_set.seed + _SHUFFLE_HASH)
                )
                monster_ai_rng_seed = _int32(
                    _uint32(self._run_rng_set.seed + _MONSTER_AI_HASH)
                )
                native.reset_run_combat_pre_shuffled(
                    self._handle,
                    deck,
                    encounter_id,
                    relics,
                    self._player_hp,
                    self._player_max_hp,
                    self._potions,
                    self._gold,
                    shuffle_rng_seed,
                    shuffle_pre_skip,
                    self._niche_calls_consumed,
                    encounter_rng_seed,
                    monster_ai_rng_seed,
                    self._combat_obs_buf,
                )
            else:
                native.reset_run_combat(
                    self._handle,
                    deck,
                    encounter_id,
                    relics,
                    self._player_hp,
                    self._player_max_hp,
                    self._potions,
                    self._gold,
                    encounter_rng_seed,
                    self._combat_obs_buf,
                )

    def _sync_run_state_from_combat_obs(self) -> None:
        self._player_hp = max(0, int(self._combat_obs_buf[0]))
        self._player_max_hp = max(1, int(self._combat_obs_buf[1]))
        self._gold = max(0, int(self._combat_obs_buf[156]))
        self._potions = [int(self._combat_obs_buf[28 + i * 2]) for i in range(3)]

    def _obs(self) -> np.ndarray:
        obs = np.zeros(RUN_OBS_SIZE, dtype=np.int32)
        obs[: native.OBS_SIZE] = np.ctypeslib.as_array(self._combat_obs_buf)
        obs[native.OBS_SIZE :] = np.array(
            [
                self._phase,
                self._floor,
                ACT_OVERGROWTH if self._act == "overgrowth" else ACT_UNDERDOCKS,
                len(self._deck),
                self._gold,
                self._player_hp,
                self._player_max_hp,
                len(self._relics),
                self._current_node_type,
                int(self._reward_cards[0]),
                int(self._reward_cards[1]),
                int(self._reward_cards[2]),
                int(self._map_node_types[0]),
                int(self._map_node_types[1]),
                int(self._map_node_types[2]),
                int(self._map_node_types[3]),
                int(self._map_choices[0]),
                int(self._map_choices[1]),
                int(self._map_choices[2]),
                int(self._map_choices[3]),
                int(self._shop_cards[0]),
                int(self._shop_cards[1]),
                int(self._shop_cards[2]),
                int(self._relic_reward),
                int(self._event_id),
                int(self._potions[0]),
                int(self._potions[1]),
                int(self._potions[2]),
                int(self._shop_relics[0]),
                int(self._shop_relics[1]),
                int(self._shop_relics[2]),
                int(self._shop_potions[0]),
                int(self._shop_potions[1]),
                int(self._shop_potions[2]),
                int(self._shop_costs[SHOP_REMOVE_ACTION]),
            ],
            dtype=np.int32,
        )
        return obs

    def _info(self) -> dict:
        return {
            "phase": self._phase,
            "floor": self._floor,
            "act": self._act,
            "deck_size": len(self._deck),
            "gold": self._gold,
            "player_hp": self._player_hp,
            "player_max_hp": self._player_max_hp,
            "potions": tuple(self._potions),
            "relics": tuple(self._relics),
            "current_node_type": self._current_node_type,
            "card_rewards": tuple(int(card_id) for card_id in self._reward_cards),
            "card_reward_upgraded": tuple(
                bool(value) for value in self._reward_upgraded
            ),
            "shop_cards": tuple(int(card_id) for card_id in self._shop_cards),
            "shop_relics": tuple(int(relic_id) for relic_id in self._shop_relics),
            "shop_potions": tuple(int(potion_id) for potion_id in self._shop_potions),
            "shop_costs": tuple(int(cost) for cost in self._shop_costs),
            "relic_reward": int(self._relic_reward),
            "neow_options": tuple(int(relic_id) for relic_id in self._neow_options),
            "silver_crucible_card_rewards_seen": self._silver_crucible_card_rewards_seen,
            "silver_crucible_treasure_seen": self._silver_crucible_treasure_seen,
            "fishing_rod_combats_seen": self._fishing_rod_combats_seen,
            "venerable_tea_set_active": self._venerable_tea_set_active,
            "winged_boots_times_used": self._winged_boots_times_used,
            "shop_removals_used": self._shop_removals_used,
            "potion_reward_odds": self._potion_reward_odds,
            "event_id": int(self._event_id),
            "map_choices": (
                tuple(
                    {
                        "node_type": int(node_type),
                        "encounter": ENCOUNTER_NAMES.get(
                            int(encounter_id), f"unknown-{encounter_id}"
                        ),
                    }
                    for node_type, encounter_id in zip(
                        self._map_node_types, self._map_choices
                    )
                    if int(node_type) != NODE_NONE
                )
                if self._phase == PHASE_MAP
                else ()
            ),
            "player_won": (
                native.player_won(self._handle) if self._handle is not None else False
            ),
            "encounter_id": (
                native.encounter_id(self._handle) if self._handle is not None else -1
            ),
            "encounter": (
                ENCOUNTER_NAMES.get(native.encounter_id(self._handle), "none")
                if self._handle is not None
                else "none"
            ),
        }

    def _combat_seed(self) -> int:
        # Matches CombatState: creature.SetUniqueMonsterHpValue uses RunState.Rng.Niche,
        # whose raw seed is _int32(_uint32(run_seed + niche_hash)).
        return _int32(_uint32(self._run_rng_set.seed + _NICHE_HASH))

    def _encounter_rng_seed(self, encounter_id: int) -> int:
        # Matches EncounterModel.GenerateMonstersWithSlots:
        #   uint seed = (uint)((int)runState.Rng.Seed + runState.TotalFloor + hash(entry))
        # The reference game's TotalFloor is 0-indexed from the first combat; the emulator's
        # self._floor starts at 1 and is incremented before _reset_combat, so the offset is -2.
        # Only SlimesWeak uses this for type selection; pass 0 for others (ignored by C#).
        if encounter_id == _SLIMES_WEAK_ENCOUNTER_ID and self._run_rng_set is not None:
            total_floor = self._floor - 2
            return _int32(
                _uint32(
                    _int32(self._run_rng_set.seed)
                    + total_floor
                    + _SLIMES_WEAK_ENTRY_HASH
                )
            )
        return 0

    def _gold_reward_for_node(self) -> int:
        # Uses PlayerRng.Rewards matching GoldReward.Populate() → Rng.NextInt(min, max+1).
        # Poverty ascension (level 3+) reduces min/max by 0.75x (int truncation).
        # Elite/Boss ranges are exact; Normal uses NextInt(7, 16) = 7 + next_int(9).
        if self._current_node_type == NODE_ELITE:
            return 26 + self._player_rng.rewards.next_int(8)  # NextInt(26, 34)
        if self._current_node_type == NODE_BOSS:
            self._player_rng.rewards.next_double()  # NextInt(100, 101) still makes a call
            return 100
        return 7 + self._player_rng.rewards.next_int(9)  # NextInt(7, 16)

    def _generate_card_rewards(self) -> np.ndarray:
        # CardReward.Populate() calls CardFactory.CreateForReward per card, which:
        #   1. Rolls rarity via PlayerOdds.CardRarity.Roll() → 1 Rewards RNG call
        #   2. Picks card via rng.NextItem() → 1 Rewards RNG call
        #   3. Rolls upgrade via RollForUpgrade() → 1 Rewards RNG call (rng.NextFloat())
        # Total: 3 Rewards RNG calls per card × 3 cards = 9 calls.
        rng = self._player_rng.rewards
        cards: list[int] = []
        for _ in range(3):
            rarity = self._roll_reward_card_rarity(rng)
            cards.append(
                self._choose_card_with_rarity(IRONCLAD_REWARD_POOL, rarity, cards, rng)
            )
            rng.next_double()  # RollForUpgrade: rng.NextFloat() → 1 Rewards RNG call
        return np.array(cards, dtype=np.int32)

    def _generate_shop_cards(self) -> np.ndarray:
        cards: list[int] = []
        for pool in [
            SHOP_ATTACK_CARDS,
            SHOP_ATTACK_CARDS,
            SHOP_SKILL_CARDS,
            SHOP_SKILL_CARDS,
            SHOP_POWER_CARDS,
        ]:
            rarity = self._roll_card_rarity(CARD_RARITY_ODDS_SHOP)
            cards.append(self._choose_card_with_rarity(pool, rarity, cards))
        for rarity in SHOP_COLORLESS_CARD_RARITIES:
            cards.append(
                self._choose_card_with_rarity(SHOP_COLORLESS_CARDS, rarity, cards)
            )
        return np.array(cards, dtype=np.int32)

    def _shop_card_cost(self, card_id: int, *, colorless: bool = False) -> int:
        rarity = CARD_RARITY_BY_ID.get(card_id, CARD_RARITY_COMMON)
        base_cost = SHOP_CARD_BASE_COSTS_BY_RARITY[rarity]
        if colorless:
            base_cost = self._round_positive(base_cost * 1.15)
        return self._round_positive(base_cost * self._rng.uniform(0.95, 1.05))

    def _roll_reward_card_rarity(self, rng: GameRng) -> int:
        if self._current_node_type == NODE_ELITE:
            odds = CARD_RARITY_ODDS_ELITE
        elif self._current_node_type == NODE_BOSS:
            odds = CARD_RARITY_ODDS_BOSS
        else:
            odds = CARD_RARITY_ODDS_REGULAR
        return self._roll_card_rarity(odds, change_future_odds=True, rng=rng)

    def _roll_card_rarity(
        self,
        odds: tuple[float, float],
        *,
        change_future_odds: bool = False,
        rng: GameRng | None = None,
    ) -> int:
        rare_odds, uncommon_odds = odds
        offset = 0.0 if odds == CARD_RARITY_ODDS_BOSS else self._card_rarity_offset
        roll = rng.next_double() if rng is not None else float(self._rng.random())
        rare_threshold = rare_odds + offset
        if roll < rare_threshold:
            rarity = CARD_RARITY_RARE
        elif roll < rare_threshold + uncommon_odds:
            rarity = CARD_RARITY_UNCOMMON
        else:
            rarity = CARD_RARITY_COMMON

        if change_future_odds:
            if rarity == CARD_RARITY_RARE:
                self._card_rarity_offset = CARD_RARITY_BASE_OFFSET
            else:
                self._card_rarity_offset = min(
                    self._card_rarity_offset + CARD_RARITY_GROWTH,
                    CARD_RARITY_MAX_OFFSET,
                )
        return rarity

    def _choose_card_with_rarity(
        self,
        pool: np.ndarray,
        rarity: int,
        blacklist: list[int],
        rng: GameRng | None = None,
    ) -> int:
        for allowed_rarity in self._rarity_fallbacks(rarity):
            available = [
                int(card_id)
                for card_id in pool
                if int(card_id) not in blacklist
                and CARD_RARITY_BY_ID.get(int(card_id), CARD_RARITY_COMMON)
                == allowed_rarity
            ]
            if available:
                if rng is not None:
                    return available[rng.next_int(len(available))]
                return int(self._rng.choice(available))
        available = [int(card_id) for card_id in pool if int(card_id) not in blacklist]
        if available:
            if rng is not None:
                return available[rng.next_int(len(available))]
            return int(self._rng.choice(available))
        if rng is not None:
            return int(pool[rng.next_int(len(pool))])
        return int(self._rng.choice(pool))

    @staticmethod
    def _rarity_fallbacks(rarity: int) -> tuple[int, int, int]:
        if rarity == CARD_RARITY_COMMON:
            return (CARD_RARITY_COMMON, CARD_RARITY_UNCOMMON, CARD_RARITY_RARE)
        if rarity == CARD_RARITY_UNCOMMON:
            return (CARD_RARITY_UNCOMMON, CARD_RARITY_RARE, CARD_RARITY_COMMON)
        return (CARD_RARITY_RARE, CARD_RARITY_COMMON, CARD_RARITY_UNCOMMON)

    def _shop_relic_cost(self, relic_id: int) -> int:
        base_cost = SHOP_RELIC_BASE_COSTS.get(relic_id, 200)
        return self._round_positive(base_cost * self._rng.uniform(0.85, 1.15))

    def _shop_potion_cost(self, potion_id: int) -> int:
        rarity = POTION_RARITY_BY_ID.get(potion_id, POTION_RARITY_COMMON)
        base_cost = SHOP_POTION_BASE_COSTS[rarity]
        return self._round_positive(base_cost * self._rng.uniform(0.95, 1.05))

    def _shop_removal_cost(self) -> int:
        return 100 + 50 * self._shop_removals_used

    def _event_gold_amount(self, base: int) -> int:
        return max(0, base + int(self._rng.integers(-15, 16)))

    @staticmethod
    def _round_positive(value: float) -> int:
        return int(value + 0.5)

    def _add_potion(self, potion_id: int) -> bool:
        for index, current in enumerate(self._potions):
            if current == 0:
                self._potions[index] = potion_id
                return True
        return False

    def _gain_max_hp(self, amount: int) -> None:
        self._player_max_hp += amount
        self._player_hp = min(self._player_max_hp, self._player_hp + amount)

    def _next_potion(self, rng: GameRng) -> int:
        return self._next_potions(1, rng)[0]

    def _next_potions(self, count: int, rng: GameRng | None = None) -> list[int]:
        # Default to Rewards RNG if none provided (usual case for non-combat rewards).
        actual_rng = rng if rng is not None else self._player_rng.rewards
        potions: list[int] = []
        for _ in range(count):
            rarity = self._roll_potion_rarity(actual_rng)
            potions.append(self._choose_potion_with_rarity(rarity, potions, actual_rng))
        return potions

    def _check_potion_roll(self, roll: float) -> bool:
        """Evaluate a pre-rolled Rewards RNG value against the current potion odds."""
        elite_bonus = (
            POTION_REWARD_ELITE_BONUS * 0.5
            if self._current_node_type == NODE_ELITE
            else 0.0
        )
        if roll < self._potion_reward_odds + elite_bonus:
            self._potion_reward_odds -= POTION_REWARD_STEP
            return True
        self._potion_reward_odds += POTION_REWARD_STEP
        return False

    def _roll_potion_reward(self) -> bool:
        return self._check_potion_roll(self._player_rng.rewards.next_double())

    def _roll_potion_rarity(self, rng: GameRng) -> int:
        roll = rng.next_double()
        if roll <= 0.1:
            return POTION_RARITY_RARE
        if roll <= 0.35:
            return POTION_RARITY_UNCOMMON
        return POTION_RARITY_COMMON

    def _choose_potion_with_rarity(
        self, rarity: int, blacklist: list[int], rng: GameRng
    ) -> int:
        available = [
            int(potion_id)
            for potion_id in POTION_REWARD_POOL
            if int(potion_id) not in blacklist
            and POTION_RARITY_BY_ID[int(potion_id)] == rarity
        ]
        if available:
            return available[rng.next_int(len(available))]
        return int(POTION_REWARD_POOL[rng.next_int(len(POTION_REWARD_POOL))])

    def _combat_deck(self) -> list[int]:
        return [card for card in self._deck if abs(card) != SPOILS_MAP_CARD]

    def _remove_lowest_priority_card(self) -> None:
        for card_id in (10001, 472, 131, 30):
            for index, encoded_card in enumerate(self._deck):
                if abs(encoded_card) == card_id:
                    del self._deck[index]
                    return
        self._deck.pop()

    def _next_relic(self) -> int:
        # Relic GrabBag uses UpFront RNG in the game, but we use a simplified pool.
        # To match RNG call count, we use UpFront RNG here.
        rng = self._run_rng_set.up_front
        available = [
            int(relic) for relic in RELIC_REWARD_POOL if int(relic) not in self._relics
        ]
        if not available:
            return int(RELIC_REWARD_POOL[rng.next_int(len(RELIC_REWARD_POOL))])
        return available[rng.next_int(len(available))]

    def _generate_neow_options(self) -> None:
        rng = self._run_rng_set.neow_rng()
        cursed = NEOW_CURSE_OPTIONS[rng.next_int(len(NEOW_CURSE_OPTIONS))]
        positive = list(NEOW_POSITIVE_OPTIONS)
        if cursed == RELIC_CURSED_PEARL:
            positive = [r for r in positive if r != RELIC_GOLDEN_PEARL]
        elif cursed == RELIC_HEFTY_TABLET:
            positive = [r for r in positive if r != RELIC_ARCANE_SCROLL]
        elif cursed == RELIC_LEAFY_POULTICE:
            positive = [r for r in positive if r != RELIC_NEW_LEAF]
        elif cursed == RELIC_PRECARIOUS_SHEARS:
            positive = [r for r in positive if r != RELIC_PRECISE_SCISSORS]
        if cursed != RELIC_LARGE_CAPSULE:
            positive.append(RELIC_LAVA_ROCK if rng.next_bool() else RELIC_SMALL_CAPSULE)
        positive.append(
            RELIC_NUTRITIOUS_OYSTER if rng.next_bool() else RELIC_STONE_HUMIDIFIER
        )
        positive.append(RELIC_NEOWS_TALISMAN if rng.next_bool() else RELIC_POMANDER)
        rng.shuffle(positive)
        self._neow_options[:] = [positive[0], positive[1], cursed]

    def _is_upgradable(self, encoded_card: int) -> bool:
        # A card is upgradable if it's positive (not already upgraded) and not
        # in the set of cards with MaxUpgradeLevel=0 (status/curse cards).
        # Matches CardModel.IsUpgradable: CurrentUpgradeLevel < MaxUpgradeLevel.
        return encoded_card > 0 and encoded_card not in NON_UPGRADABLE_CARD_IDS

    def _upgrade_first_card(self) -> bool:
        for index, encoded_card in enumerate(self._deck):
            if self._is_upgradable(encoded_card):
                self._deck[index] = -encoded_card
                return True
        return False

    def _upgrade_last_card_matching(self, card_id: int) -> bool:
        for index in range(len(self._deck) - 1, -1, -1):
            if self._deck[index] == card_id and self._is_upgradable(self._deck[index]):
                self._deck[index] = -card_id
                return True
        return False

    def _upgrade_random_card(self) -> bool:
        indexes = [
            index
            for index, encoded_card in enumerate(self._deck)
            if self._is_upgradable(encoded_card)
        ]
        if not indexes:
            return False
        # Upgrading random card (e.g. Fishing Rod) uses UpFront RNG in the game.
        index = indexes[self._run_rng_set.up_front.next_int(len(indexes))]
        self._deck[index] = -self._deck[index]
        return True

    def _transform_first_card_matching(self, card_id: int) -> bool:
        for index, encoded_card in enumerate(self._deck):
            if abs(encoded_card) == card_id:
                # Transformation uses transformations RNG.
                self._deck[index] = int(
                    IRONCLAD_REWARD_POOL[
                        self._player_rng.transformations.next_int(
                            len(IRONCLAD_REWARD_POOL)
                        )
                    ]
                )
                return True
        return False

    def _transform_first_card(self) -> None:
        if not self._deck:
            raise ValueError("No card available to transform.")
        # Transformation uses transformations RNG.
        self._deck[0] = int(
            IRONCLAD_REWARD_POOL[
                self._player_rng.transformations.next_int(len(IRONCLAD_REWARD_POOL))
            ]
        )
