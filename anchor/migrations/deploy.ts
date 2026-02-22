import * as anchor from "@coral-xyz/anchor";
import { Program } from "@coral-xyz/anchor";
import { Nereon } from "../target/types/nereon";

module.exports = async function (provider: anchor.AnchorProvider) {
  anchor.setProvider(provider);
  const program = anchor.workspace.Nereon as Program<Nereon>;
  console.log("NEREON program deployed. Program ID:", program.programId.toString());
};
