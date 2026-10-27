-- FullyVolted results database. Run once in Supabase: Dashboard > SQL Editor > New query > paste > Run.
-- Safe to re-run: everything is "if not exists" / "or replace".

-- One row per attempt at the procedure. The id is made in Unity (a GUID), so the game never has to
-- read anything back before it can save the steps.
create table if not exists public.runs (
    id          uuid primary key,
    trainee     text not null default 'Trainee',
    started_at  timestamptz not null default now()
);

-- One row per finished part of a run: the split time and how many mistakes were made in it.
create table if not exists public.step_results (
    id          bigint generated always as identity primary key,
    run_id      uuid not null references public.runs (id) on delete cascade,
    step_order  int  not null,                 -- 1 = PpeCheck ... 5 = DescendToBottom
    step_name   text not null,
    seconds     numeric(10, 3) not null check (seconds >= 0),
    mistakes    int  not null default 0 check (mistakes >= 0),
    correct     boolean not null,              -- false = the part was finished, but not safely
    created_at  timestamptz not null default now(),
    unique (run_id, step_order)
);

create index if not exists step_results_run_id_idx on public.step_results (run_id);

-- The custom query (contract: JOIN + aggregate). One row per run with its totals - the "leaderboard" view.
-- security_invoker: the view obeys the same row level security as the tables underneath it.
create or replace view public.run_summary with (security_invoker = true) as
select
    r.id                                   as run_id,
    r.trainee,
    r.started_at,
    count(s.id)                            as steps_done,
    count(s.id) = 5                        as finished,
    coalesce(sum(s.seconds), 0)            as total_seconds,
    coalesce(sum(s.mistakes), 0)           as total_mistakes,
    coalesce(bool_and(s.correct), false)   as all_correct,
    (array_agg(s.step_name order by s.seconds desc))[1] as slowest_step
from public.runs r
left join public.step_results s on s.run_id = r.id
group by r.id, r.trainee, r.started_at;

-- Row level security: the anon key (the one in the game and the website) may add results and read them,
-- but never change or delete them.
alter table public.runs         enable row level security;
alter table public.step_results enable row level security;

drop policy if exists "anyone can read runs"           on public.runs;
drop policy if exists "game can add runs"              on public.runs;
drop policy if exists "anyone can read step results"   on public.step_results;
drop policy if exists "game can add step results"      on public.step_results;

create policy "anyone can read runs"         on public.runs         for select to anon, authenticated using (true);
create policy "game can add runs"            on public.runs         for insert to anon, authenticated with check (true);
create policy "anyone can read step results" on public.step_results for select to anon, authenticated using (true);
create policy "game can add step results"    on public.step_results for insert to anon, authenticated with check (true);

grant select, insert on public.runs, public.step_results to anon, authenticated;
grant select on public.run_summary to anon, authenticated;
