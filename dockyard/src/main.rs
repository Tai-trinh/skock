mod catalog;
mod protocol;
mod session;

use std::io::{self, BufRead, Write};

use protocol::InMessage;
use session::Session;

fn main() -> anyhow::Result<()> {
    let stdin = io::stdin();
    let stdout = io::stdout();
    let mut out = io::BufWriter::new(stdout.lock());

    let mut session: Option<Session> = None;

    for line in stdin.lock().lines() {
        let line = line?;
        if line.trim().is_empty() {
            continue;
        }

        let msg: InMessage = match serde_json::from_str(&line) {
            Ok(m) => m,
            Err(e) => {
                eprintln!("RESULT:{{\"error\":\"invalid_json\",\"message\":\"{e}\"}}");
                return Ok(());
            }
        };

        let response = match msg {
            InMessage::GetOffers(payload) => {
                let s = Session::new(payload);
                let resp = s.get_offers_response();
                session = Some(s);
                resp
            }
            InMessage::Commission { blueprint_id } => match &mut session {
                Some(s) => s.commission(&blueprint_id),
                None => protocol::OutMessage::err("session_not_started"),
            },
            InMessage::SalvageFleetShip { index } => match &mut session {
                Some(s) => s.salvage_fleet_ship(index),
                None => protocol::OutMessage::err("session_not_started"),
            },
            InMessage::RerollTier { tier_index } => match &mut session {
                Some(s) => s.reroll_tier(tier_index),
                None => protocol::OutMessage::err("session_not_started"),
            },
            InMessage::RerollResearch { track_index } => match &mut session {
                Some(s) => s.reroll_research(track_index),
                None => protocol::OutMessage::err("session_not_started"),
            },
            InMessage::BuyResearch { upgrade_id } => match &mut session {
                Some(s) => s.buy_research(&upgrade_id),
                None => protocol::OutMessage::err("session_not_started"),
            },
            InMessage::ShoppingDone => {
                let resp = match &session {
                    Some(s) => s.shopping_done(),
                    None => protocol::OutMessage::err("session_not_started"),
                };
                write_line(&mut out, &resp)?;
                break;
            }
        };

        write_line(&mut out, &response)?;
    }

    Ok(())
}

fn write_line(out: &mut impl Write, msg: &protocol::OutMessage) -> anyhow::Result<()> {
    serde_json::to_writer(&mut *out, msg)?;
    out.write_all(b"\n")?;
    out.flush()?;
    Ok(())
}
