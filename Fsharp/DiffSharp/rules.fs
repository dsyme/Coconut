﻿module rules

open Microsoft.FSharp.Quotations
open Quotations.DerivedPatterns
open types

(*
// Inspired by: https://github.com/jrh13/hol-light/blob/master/nets.ml
module termnet = 
  type TermLabel = Vnet
                 | Cnet of (string * int)
                 | Lnet of int
  type 'a Net = NetNode of (TermLabel * 'a Net) List * 'a List
*)

type Rule = Expr -> Expr Option

let (|ApplicableRule|_|) (rs: Rule List) (e: Expr): (Rule) Option = 
  List.tryFind (fun r -> not(Option.isNone(r e))) rs

let letInliner (e: Expr): Expr Option = 
  match e with 
  | Patterns.Let(v, e1, e2) -> Some(e2.Substitute(fun v2 -> if v = v2 then Some(e1) else None))
  | _ -> None

let divide2Mult (e: Expr): Expr Option = 
   match e with 
   | SpecificCall  <@ (/) @> (None, _, [SpecificCall  <@ (/) @> (None, _, [a; b]) ; c]) -> 
     Some(<@@ (%%a: Number) / ((%%b: Number) * (%%c: Number)) @@>)
   | _ -> None
(*
let awfdivide2Mult (e: Expr): Expr Option = 
   Rule <@ (a: Number) / (b: Number) @> (<@@ (%%a: Number) / ((%%b: Number) * (%%c: Number)) @@>)
*)
   

let distrMult (e: Expr): Expr Option = 
   match e with 
   | SpecificCall  <@ (*) @> (None, _, [a; SpecificCall  <@ (+) @> (None, _, [b; c])]) -> 
     Some(<@@ ((%%a: Number) * (%%b: Number)) + ((%%a: Number) * (%%c: Number)) @@>)
   | _ -> None

let constFold1 (e: Expr): Expr Option = 
   match e with 
   | SpecificCall  <@ (*) @> (None, _, [a; b]) when b = <@@ 1. @@> -> 
     Some(a)
   | _ -> None
(*
let awfx (e:Expr) = 
   let (a : Number) = Number 0 in (<@ a * 1. === a @>)
*)

let multDivide (e: Expr): Expr Option = 
   match e with 
   | SpecificCall  <@ (*) @> (None, _, [a; SpecificCall  <@ (/) @> (None, _, [b; c])]) when c = a && b = <@@ 1. @@> -> 
     Some(<@@ 1. @@>)
   | _ -> None

let s_1 = Expr.Cast<Number>(Expr.Var(Var.Global("s1", typeof<Number>)))
let s_2 = Expr.Cast<Number>(Expr.Var(Var.Global("s2", typeof<Number>)))
let s_3 = Expr.Cast<Number>(Expr.Var(Var.Global("s3", typeof<Number>)))
(*
let v_1: Vector = [| s_1 |]
let v_2: Vector = [| s_1 |]
let m_1: Matrix = [| v_1 |]
let m_2: Matrix = [| v_1 |]
*)
let scalarMetaVars = List.map (fun (x: Expr<Number>) -> x.Raw) [s_1; s_2; s_3]
let (<==>) (s1: 'a) (s2: 'a):'a = s1

let divide2Mult_2 = 
  <@ 
    (%s_1 / %s_2) / %s_3
    <==> 
    %s_1 / (%s_2 * %s_3)
  @>

let distrMult_2 = 
  <@
    %s_1 * (%s_2 + %s_3)
    <==>
    %s_1 * %s_2 + %s_1 * %s_3
  @>

let constFold1_2 = 
  <@ 
    (%s_1 * 1.)
    <==>
    (%s_1)
  @>

let multDivide_2 = 
  <@
    %s_1 * (1. / %s_2)
    <==>
    1.
  @>, 
  <@ %s_1 = %s_2 @>

let compilePatternWithPreconditionToRule(pat: Expr, precondition: Expr): Rule =
  let rec extractList(pats: Expr List, exprs: Expr List): (Var * Expr) List Option = 
    let vars = List.map2 (fun vp ve -> extract(vp, ve)) pats exprs
    if(List.forall (Option.isSome) vars) then
      Some(List.concat (List.map (Option.get) vars))
    else
      None
  and extract(p: Expr, e: Expr): (Var * Expr) List Option = 
    match (p, e) with
    | (Patterns.Var(v), _) when List.exists (fun x -> x = p) scalarMetaVars -> 
      Some([v, e])
    | (Patterns.Call(None, op, pats), Patterns.Call(None, oe, exprs)) when (List.length pats) = (List.length exprs) && op = oe ->
        extractList(pats, exprs)
    | (ExprShape.ShapeCombination(op, pats), ExprShape.ShapeCombination(oe, exprs)) when (List.length pats) = (List.length exprs) && op = oe ->
        extractList(pats, exprs)
    | (Patterns.Value(v1), Patterns.Value(v2)) when v1 = v2 -> Some([])
    | _ -> None
  let unifiedVars = 
    let rec processPrecondition(pre: Expr): (Var * Var) List =
      match pre with 
      | Patterns.Value(v, _) when v.Equals(true) -> []
      | SpecificCall  <@ (=) @> (None, _, [Patterns.Var(v1) as a; Patterns.Var(v2) as b]) when 
          (List.exists ((=) a) scalarMetaVars) && (List.exists ((=) b) scalarMetaVars) -> [v1, v2]
      | SpecificCall  <@ (&&) @> (None, _, [a; b]) -> List.append (processPrecondition a) (processPrecondition b)
      | _ -> failwith (sprintf "Cannot parse the precondition %A" pre)
    processPrecondition(precondition)
  let unification(values: (Var * Expr) List, unifiedVars: (Var * Var) List): (Var * Expr) List Option = 
    List.fold (fun accOpt (curv, cure) -> 
        Option.bind (fun acc -> 
            let sameVariables = curv :: List.collect (fun (v1, v2) -> if(v1 = curv) then [v2] elif (v2 = curv) then [v1] else []) unifiedVars
            let expressions = List.collect (fun (v, e) -> List.collect (fun v1 -> if(v1 = v) then [e] else []) sameVariables) acc
            let allAreTheSame = List.forall (fun e -> e = cure) expressions
            if(allAreTheSame) then
              Some(List.append acc [curv, cure])
            else 
              None
        ) accOpt
    ) (Some([])) values
  fun (exp: Expr) ->
    let (boundVarsOpt, rhs) = 
      match pat with 
      | SpecificCall <@ (<==>) @> (None, _, [p; rhs]) -> 
        printfn "pattern is %A and rhs is %A" p rhs
        extract(p, exp), rhs
      | _ -> failwith "Rewrite patterns should be of the form `lhs <==> rhs`"
    Option.bind (fun boundVarsInit -> 
      let unifiedBoundVars = unification(boundVarsInit, unifiedVars)
      Option.map (fun boundVars ->
        rhs.Substitute(fun v -> 
          Option.map (fun (_, e) -> e) 
            (List.tryFind (fun (v1, _) -> v = v1) boundVars))
      ) unifiedBoundVars
    ) boundVarsOpt
      

let compilePatternToRule(pat: Expr): Rule =
  compilePatternWithPreconditionToRule(pat, <@ true @>)

let divide2Mult_3: Rule = compilePatternToRule divide2Mult_2
let distrMult_3: Rule = compilePatternToRule distrMult_2
let constFold1_3: Rule = compilePatternToRule constFold1_2
let multDivide_3: Rule = compilePatternWithPreconditionToRule multDivide_2