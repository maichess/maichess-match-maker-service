Feature: MatchingService — background match-making logic

  Scenario: Queue has fewer than 2 players — no match attempted
    Given the "5+0" queue has fewer than 2 players
    When the matching service processes the "5+0" queue
    Then no create-match call is made
    And no dequeue is attempted
    And no exception is thrown

  Scenario: Dequeue returns fewer than 2 tokens — no match attempted
    Given the "5+0" queue reports 2 or more players
    And the dequeue returns 0 tokens
    When the matching service processes the "5+0" queue
    Then no create-match call is made
    And no exception is thrown

  Scenario: Both entries present — match created and both tokens marked matched
    Given the "5+0" queue reports 2 or more players
    And the dequeue returns tokens "t1" and "t2"
    And the entry for "t1" belongs to user "user-a"
    And the entry for "t2" belongs to user "user-b"
    And the match manager creates match "match-xyz"
    When the matching service processes the "5+0" queue
    Then a create-match call is made with white "user-a" and black "user-b"
    And "t1" is marked matched with user "user-a" and match "match-xyz"
    And "t2" is marked matched with user "user-b" and match "match-xyz"

  Scenario: First entry is missing — warning logged and no match created
    Given the "5+0" queue reports 2 or more players
    And the dequeue returns tokens "t1" and "t2"
    And the entry for "t1" is missing
    And the entry for "t2" belongs to user "user-b"
    When the matching service processes the "5+0" queue
    Then no create-match call is made
    And a warning is logged
    And no error is logged
    And no exception is thrown

  Scenario: Second entry is missing — warning logged and no match created
    Given the "5+0" queue reports 2 or more players
    And the dequeue returns tokens "t1" and "t2"
    And the entry for "t1" belongs to user "user-a"
    And the entry for "t2" is missing
    When the matching service processes the "5+0" queue
    Then no create-match call is made
    And a warning is logged
    And no error is logged
    And no exception is thrown

  Scenario: Match Manager throws an exception — error logged and no tokens marked matched
    Given the "5+0" queue reports 2 or more players
    And the dequeue returns tokens "t1" and "t2"
    And the entry for "t1" belongs to user "user-a"
    And the entry for "t2" belongs to user "user-b"
    And the match manager throws a gRPC exception
    When the matching service processes the "5+0" queue
    Then no tokens are marked matched
    And an error is logged

  Scenario: Match Manager throws and cancellation is requested — exception propagates
    Given the "5+0" queue reports 2 or more players
    And the dequeue returns tokens "t1" and "t2"
    And the entry for "t1" belongs to user "user-a"
    And the entry for "t2" belongs to user "user-b"
    And the match manager throws a gRPC exception
    And the cancellation token is cancelled
    When the matching service processes the "5+0" queue
    Then the exception propagates

  Scenario Outline: The queue's time format id is forwarded to the create-match call
    Given the "<timeFormatId>" queue reports 2 or more players
    And the dequeue returns tokens "t1" and "t2"
    And the entry for "t1" belongs to user "user-a"
    And the entry for "t2" belongs to user "user-b"
    And the match manager creates match "match-x"
    When the matching service processes the "<timeFormatId>" queue
    Then the create-match call uses time format id "<timeFormatId>"

    Examples:
      | timeFormatId |
      | 1+0          |
      | 5+0          |
      | 5+3          |
      | 30+20        |
