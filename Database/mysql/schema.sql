-- FullyVolted results database, MySQL 8 version (our own database; the Supabase one is ../schema.sql).
-- Run by Api/init_db.py as the fullyvolted_api user. Safe to re-run: "if not exists" / "or replace".

-- One row per attempt at the procedure. The id is made in Unity (a GUID), so the game never has to
-- read anything back before it can save the steps.
create table if not exists runs (
    id          char(36)     not null primary key,
    trainee     varchar(100) not null default 'Trainee',
    started_at  datetime(3)  not null default current_timestamp(3)
);

-- One row per finished part of a run: the split time and how many mistakes were made in it.
create table if not exists step_results (
    id          bigint        not null auto_increment primary key,
    run_id      char(36)      not null,
    step_order  int           not null,             -- 1 = PpeCheck ... 5 = DescendToBottom
    step_name   varchar(50)   not null,
    seconds     decimal(10,3) not null,
    mistakes    int           not null default 0,
    correct     boolean       not null,             -- false = the part was finished, but not safely
    created_at  datetime(3)   not null default current_timestamp(3),
    constraint step_results_run_fk  foreign key (run_id) references runs (id) on delete cascade,
    constraint step_results_once    unique (run_id, step_order),
    constraint step_results_seconds check (seconds >= 0),
    constraint step_results_mistakes check (mistakes >= 0)
);

-- One row per mistake: the plain-language reason the game gave ("Did Ground And Lock before Switch Off").
-- The id keeps them in the order they happened. Deleted together with their step.
create table if not exists step_mistakes (
    id          bigint        not null auto_increment primary key,
    run_id      char(36)      not null,
    step_order  int           not null,
    reason      varchar(255)  not null,
    constraint step_mistakes_step_fk foreign key (run_id, step_order)
        references step_results (run_id, step_order) on delete cascade
);

-- Who is at the station right now: one row, set from the portal when a trainee signs in with their name,
-- read by the game when a run starts. The game never needs a keyboard in VR.
create table if not exists active_trainee (
    id      tinyint      not null primary key default 1,
    name    varchar(100) not null,
    set_at  datetime(3)  not null default current_timestamp(3),
    constraint active_trainee_single check (id = 1)
);

-- The custom query (contract: JOIN + aggregate). One row per run with its totals.
-- MySQL has no bool_and / array_agg like Postgres, so: all steps correct = the lowest "correct" is 1,
-- and the slowest step comes from a small sub-query per run.
create or replace view run_summary as
select
    r.id                              as run_id,
    r.trainee,
    r.started_at,
    count(s.id)                       as steps_done,
    count(s.id) = 5                   as finished,
    coalesce(sum(s.seconds), 0)       as total_seconds,
    coalesce(sum(s.mistakes), 0)      as total_mistakes,
    coalesce(min(s.correct), 0) = 1   as all_correct,
    (select s2.step_name
       from step_results s2
      where s2.run_id = r.id
      order by s2.seconds desc
      limit 1)                        as slowest_step
from runs r
left join step_results s on s.run_id = r.id
group by r.id, r.trainee, r.started_at;
