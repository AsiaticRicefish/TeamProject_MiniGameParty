// using System.Collections.Generic;
// using Cysharp.Threading.Tasks;
//
// namespace Store
// {
//     //구매 가능한(파는) 상품 카테고리
//     public enum StockType
//     {
//         Character,
//         Equip,
//     }
//
//
//     //구매 항목에 대한 정의가 필요함
//     public struct PurchaseItem
//     {
//         public StockType type;
//         public string itemId;
//         
//         public int Quantity; 
//         public PurchaseLine(SkuRef s,int q=1){Sku=s;Quantity=q;} 
//     }
//     
//     public enum PurchaseError 
//     { 
//         None, 
//         AlreadyOwned, NotEnoughCurrency, ItemNotFound, 
//         Conflict, 
//         Network, 
//         Unknown 
//     }
//
//
//     // stock data = item data로 퉁칩시다 ^^ 아닌가요? 아닙니다 아 
//
//     public struct PurchaseResult
//     {
//     }
//
//     public struct PurchaseQuote
//     {
//     }
//
//     public interface IPurchaseService
//     {
//         UniTask<PurchaseQuote> QuoteAsync(IEnumerable<PurchaseLine> lines);
//         UniTask<PurchaseResult> PurchaseAsync(IEnumerable<PurchaseLine> lines, bool autoEquip = false);
//     }
// }