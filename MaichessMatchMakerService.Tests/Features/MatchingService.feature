Feature: MatchingService — background match-making logic

  Scenario: Queue has fewer than 2 players — no match attempted
    Given the "blitz" queue has fewer than 2 players
    When the matching service processes the "blitz" queue
    Then no CreateMatch gRPC call is made

  Scenario: Dequeue returns fewer than 2 tokens — no match attempted
    Given the "blitz" queue reports 2 or more players
    And the dequeue returns 0 tokens
    When the matching service processes the "blitz" queue
    Then no CreateMatch gRPC call is made

  Scenario: Both entries present — match created and both tokens marked matched
    Given the "blitz" queue reports 2 or more players
    And the dequeue returns tokens "t1" and "t2"
    And the entry for "t1" belongs to user "user-a"
    And the entry for "t2" belongs to user "user-b"
    And the match manager creates match "match-xyz"
    When the matching service processes the "blitz" queue
    Then a CreateMatch gRPC call is made with white "user-a" and black "user-b"
    And "t1" is marked matched with user "user-a" and match "match-xyz"
    And "t2" is marked matched with user "user-b" and match "match-xyz"

  Scenario: First entry is missing — warning logged and no match created
    Given the "blitz" queue reports 2 or more players
    And the dequeue returns tokens "t1" and "t2"
    And the entry for "t1" is missing
    And the entry for "t2" belongs to user "user-b"
    When the matching service processes the "blitz" queue
    Then no CreateMatch gRPC call is made
    And a warning is logged

  Scenario: Second entry is missing — warning logged and no match created
    Given the "blitz" queue reports 2 or more players
    And the dequeue returns tokens "t1" and "t2"
    And the entry for "t1" belongs to user "user-a"
    And the entry for "t2" is missing
    When the matching service processes the "blitz" queue
    Then no CreateMatch gRPC call is made
    And a warning is logged

  Scenario: Match Manager throws an exception — error logged and no tokens marked matched
    Given the "blitz" queue reports 2 or more players
    And the dequeue returns tokens "t1" and "t2"
    And the entry for "t1" belongs to user "user-a"
    And the entry for "t2" belongs to user "user-b"
    And the match manager throws a gRPC exception
    When the matching service processes the "blitz" queue
    Then no tokens are marked matched
    And an error is logged

  Scenario: Match Manager throws and cancellation is requested — exception propagates
    Given the "blitz" queue reports 2 or more players
    And the dequeue returns tokens "t1" and "t2"
    And the entry for "t1" belongs to user "user-a"
    And the entry for "t2" belongs to user "user-b"
    And the match manager throws a gRPC exception
    And the cancellation token is cancelled
    When the matching service processes the "blitz" queue
    Then the exception propagates

  Scenario Outline: Time control string maps to correct gRPC enum value
    Given the "<timeControl>" queue reports 2 or more players
    And the dequeue returns tokens "t1" and "t2"
    And the entry for "t1" belongs to user "user-a"
    And the entry for "t2" belongs to user "user-b"
    And the match manager creates match "match-x"
    When the matching service processes the "<timeControl>" queue
    Then the CreateMatch request uses time control <expectedEnum>

    Examples:
      | timeControl | expectedEnum |
      | bullet      | Bullet       |
      | blitz       | Blitz        |
      | rapid       | Rapid        |
      | classical   | Classical    |
      | unknown     | Unspecified  |
