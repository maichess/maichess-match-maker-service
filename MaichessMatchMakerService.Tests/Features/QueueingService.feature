Feature: QueueingService — queue entry, status, and dequeue logic

  # ── EnqueueAsync: validation ───────────────────────────────────────────────

  Scenario: Invalid time format id — returns invalid input
    When enqueue is called with userId "user-a" timeFormatId "invalid" opponentType "human" and botId ""
    Then the enqueue result is invalid input "invalid time_format_id"

  Scenario: Invalid opponent type — returns invalid input
    When enqueue is called with userId "user-a" timeFormatId "5+0" opponentType "alien" and botId ""
    Then the enqueue result is invalid input "opponent.type must be 'human' or 'bot'"

  Scenario: Bot match with missing bot_id — returns invalid input
    When enqueue is called with userId "user-a" timeFormatId "5+0" opponentType "bot" and botId ""
    Then the enqueue result is invalid input "opponent.bot_id is required for bot matches"

  Scenario: User already in queue — returns already queued
    Given the user "user-a" is already in a queue
    When enqueue is called with userId "user-a" timeFormatId "5+0" opponentType "human" and botId ""
    Then the enqueue result is already queued

  # ── EnqueueAsync: success paths ────────────────────────────────────────────

  Scenario: Human match — enqueues and returns queue token
    Given the user "user-a" is not in any queue
    When enqueue is called with userId "user-a" timeFormatId "5+0" opponentType "human" and botId ""
    Then the enqueue result is success with a queue token
    And EnqueueAsync is called for user "user-a" with time format id "5+0"

  Scenario: Bot match — calls gRPC, stores entry, and returns queue token
    Given the user "user-a" is not in any queue
    And the match manager creates match "match-xyz"
    When enqueue is called with userId "user-a" timeFormatId "5+0" opponentType "bot" and botId "bot-1"
    Then the enqueue result is success with a queue token
    And the CreateMatch gRPC request has white userId "user-a" and black botId "bot-1"
    And EnqueueBotMatchAsync is called for user "user-a" with time format id "5+0" and match "match-xyz"

  Scenario Outline: Bot match propagates the resolved time format to the gRPC request
    Given the user "user-a" is not in any queue
    And the match manager creates match "match-x"
    When enqueue is called with userId "user-a" timeFormatId "<timeFormatId>" opponentType "bot" and botId "bot-1"
    Then the CreateMatch request uses time format id "<timeFormatId>" with base <baseMs> and increment <incrementMs>

    Examples:
      | timeFormatId | baseMs  | incrementMs |
      | 1+0          | 60000   | 0           |
      | 3+2          | 180000  | 2000        |
      | 5+0          | 300000  | 0           |
      | 10+5         | 600000  | 5000        |
      | 30+20        | 1800000 | 20000       |

  # ── Bot-vs-bot match creation ─────────────────────────────────────────────

  Scenario: Bot-vs-bot match — calls gRPC with two bot players
    Given the match manager creates match "match-bvb"
    When a bot-vs-bot match is created with white "bot-a" black "bot-b" time format "5+0"
    Then the enqueue result is success with match id "match-bvb"
    And the CreateMatch gRPC request has white botId "bot-a" and black botId "bot-b"

  Scenario: Bot-vs-bot match — invalid time format id is rejected
    When a bot-vs-bot match is created with white "bot-a" black "bot-b" time format "invalid"
    Then the enqueue result is invalid input "invalid time_format_id"

  Scenario: Bot-vs-bot match — empty bot id is rejected
    When a bot-vs-bot match is created with white "" black "bot-b" time format "5+0"
    Then the enqueue result is invalid input "bot ids are required"

  # ── GetStatusAsync ─────────────────────────────────────────────────────────

  Scenario: Status — token not found — returns not found
    Given the queue entry "tok-1" does not exist
    When get status is called for token "tok-1" by user "user-a"
    Then the get status result is not found

  Scenario: Status — token belongs to different user — returns not found
    Given the queue entry "tok-1" belongs to user "user-b" and is waiting
    When get status is called for token "tok-1" by user "user-a"
    Then the get status result is not found

  Scenario: Status — entry is waiting — returns waiting with no match id
    Given the queue entry "tok-1" belongs to user "user-a" and is waiting
    When get status is called for token "tok-1" by user "user-a"
    Then the get status result is found with status "waiting" and no match id

  Scenario: Status — entry is matched — returns matched with match id
    Given the queue entry "tok-1" belongs to user "user-a" and is matched with match "match-xyz"
    When get status is called for token "tok-1" by user "user-a"
    Then the get status result is found with status "matched" and match id "match-xyz"

  # ── DequeueAsync ───────────────────────────────────────────────────────────

  Scenario: Dequeue — token not found — returns not found
    Given the queue entry "tok-1" does not exist
    When dequeue is called for token "tok-1" by user "user-a"
    Then the dequeue result is not found

  Scenario: Dequeue — token belongs to different user — returns not found
    Given the queue entry "tok-1" belongs to user "user-b" and is waiting
    When dequeue is called for token "tok-1" by user "user-a"
    Then the dequeue result is not found

  Scenario: Dequeue — valid request — removes entry and returns success
    Given the queue entry "tok-1" belongs to user "user-a" and is waiting
    When dequeue is called for token "tok-1" by user "user-a"
    Then the dequeue result is success
    And RemoveAsync is called for token "tok-1" user "user-a"
